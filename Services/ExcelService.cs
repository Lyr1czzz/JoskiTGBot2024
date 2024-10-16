using OfficeOpenXml;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace JoskiTGBot2024.Services
{
    public class ExcelService
    {
        public List<Dictionary<string, List<string>>> ProcessExcelFile(Stream fileStream)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var schedule = new List<Dictionary<string, List<string>>>(); // Возвращаемый тип: список словарей

            using (var package = new ExcelPackage(fileStream))
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
                int rows = worksheet.Dimension.Rows;
                int cols = worksheet.Dimension.Columns;

                // Проходим по всем строкам таблицы
                for (int row = 1; row <= rows; row++)
                {
                    // Проходим по каждому столбцу
                    for (int col = 1; col <= cols; col++)
                    {
                        string cellValue = worksheet.Cells[row, col].Text;
                        string date = worksheet.Cells[1, 2].Text;


                        // Проверяем, является ли ячейка названием группы
                        if (!string.IsNullOrEmpty(cellValue) && (Regex.IsMatch(cellValue, @"^[А-ЯЁ]{1,2}-\d{4}$") || Regex.IsMatch(cellValue, @"^[А-ЯЁ][а-яё]+ [А-ЯЁ]\.[А-ЯЁ]\.$")))
                        {
                            string groupName = cellValue;
                            var groupLessons = new List<string>();

                            int lessonCount = 0;  // Считаем уроки

                            int nextTeach = 0;
                            for (int nextRow = row + 1; nextRow <= row + 8 && nextRow <= rows; nextRow++)  // Читаем 14 строк (по два урока на строку, если нет 1ч или 2ч)
                            {
                                string tmp = worksheet.Cells[nextRow, col].Text;

                                if (!Regex.IsMatch(tmp, @"^[А-Я]{1,2}-\d{4}$") && !Regex.IsMatch(tmp, @"^[А-ЯЁ][а-яё]+ [А-ЯЁ]\.[А-ЯЁ]\.$"))
                                {
                                    nextTeach++;
                                }
                                else break;
                            }
                            string data = worksheet.Cells[2, 1].Text;
                            groupLessons.Add(data);
                            // Читаем следующие строки с уроками
                            for (int nextRow = row + 1; nextRow <= row + nextTeach && nextRow <= rows; nextRow++)  // Читаем 14 строк (по два урока на строку, если нет 1ч или 2ч)
                            {
                                string lesson = worksheet.Cells[nextRow, col].Text;
                                

                                if (!string.IsNullOrEmpty(lesson)) // Проверяем если нет уроков в ячейке
                                {
                                   
                                    var splited_lesson = lesson.Split('\n');
                                    var length = splited_lesson.Length;

                                    foreach (var item in splited_lesson)
                                    {
                                        item.Trim();
                                    }

                                    //if (splited_lesson[0].Contains("Физич. культ. и здор. Алешко Н.Г. (600-01)"))
                                    //{
                                    //    splited_lesson[0] += "💩💩💩(окошко)";
                                    //}
                                    //else if (splited_lesson[1].Contains("Физич. культ. и здор. Алешко Н.Г. (600-01)"))
                                    //{
                                    //    splited_lesson[1] += "💩💩💩(окошко)";
                                    //}

                                    if (length == 1)
                                    {
                                        if (splited_lesson[0].Contains("2ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n окошко");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0]}");
                                        }
                                        else if (splited_lesson[0].Contains("1ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n окошко");
                                        }
                                        else
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{lesson}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{lesson}");
                                        }
                                    }
                                    else if (length == 2)
                                    {
                                        if (splited_lesson[0].Contains("1ч") && splited_lesson[1].Contains("2ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[1]}");
                                        }
                                        else if (splited_lesson[0].Contains("2ч") && splited_lesson[1].Contains("1ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[1]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0]}");
                                        }
                                        else if (splited_lesson[0].Contains("1ч") && splited_lesson[1].Contains("1ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0] + '\n' + splited_lesson[1]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n окошко");
                                        }
                                        else if (splited_lesson[0].Contains("2ч") && splited_lesson[1].Contains("2ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n окошко");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0] + '\n' + splited_lesson[1]}");
                                        }
                                        else
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0] + '\n' + splited_lesson[1]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0] + '\n' + splited_lesson[1]}");
                                        }
                                    }
                                    else if (length == 3)
                                    {
                                        if (splited_lesson[0].Contains("1ч") && splited_lesson[1].Contains("1ч") && splited_lesson[2].Contains("2ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0] + '\n' + splited_lesson[1]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[2]}");
                                        }
                                        else if (splited_lesson[0].Contains("2ч") && splited_lesson[1].Contains("2ч") && splited_lesson[2].Contains("1ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[2]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0] + '\n' + splited_lesson[1]}");
                                        }
                                        else if (splited_lesson[0].Contains("1ч") && splited_lesson[1].Contains("2ч") && splited_lesson[2].Contains("1ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0] + '\n' + splited_lesson[2]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[1]}");
                                        }
                                        else if (splited_lesson[0].Contains("2ч") && splited_lesson[1].Contains("1ч") && splited_lesson[2].Contains("2ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[1]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0] + '\n' + splited_lesson[2]}");
                                        }
                                        else if (splited_lesson[0].Contains("1ч") && splited_lesson[1].Contains("2ч") && splited_lesson[2].Contains("2ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[1] + '\n' + splited_lesson[2]}");
                                        }
                                        else if (splited_lesson[0].Contains("2ч") && splited_lesson[1].Contains("1ч") && splited_lesson[2].Contains("1ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[1] + '\n' + splited_lesson[2]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0]}");
                                        }
                                    }
                                    else
                                    {
                                        if (splited_lesson[0].Contains("1ч") && splited_lesson[1].Contains("1ч") && splited_lesson[2].Contains("2ч") && splited_lesson[3].Contains("2ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0] + '\n' + splited_lesson[1]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[2] + '\n' + splited_lesson[3]}");
                                        }
                                        else if (splited_lesson[0].Contains("2ч") && splited_lesson[1].Contains("2ч") && splited_lesson[2].Contains("1ч") && splited_lesson[3].Contains("1ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[2] + '\n' + splited_lesson[3]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0] + '\n' + splited_lesson[1]}");
                                        }
                                        else if (splited_lesson[0].Contains("1ч") && splited_lesson[1].Contains("2ч") && splited_lesson[2].Contains("1ч") && splited_lesson[3].Contains("2ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0] + '\n' + splited_lesson[2]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[1] + '\n' + splited_lesson[3]}");
                                        }
                                        else if (splited_lesson[0].Contains("2ч") && splited_lesson[1].Contains("1ч") && splited_lesson[2].Contains("2ч") && splited_lesson[3].Contains("1ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[1] + '\n' + splited_lesson[3]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0] + '\n' + splited_lesson[2]}");
                                        }
                                        else if (splited_lesson[0].Contains("2ч") && splited_lesson[1].Contains("1ч") && splited_lesson[2].Contains("1ч") && splited_lesson[3].Contains("2ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[1] + '\n' + splited_lesson[2]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0] + '\n' + splited_lesson[3]}");
                                        }
                                        else if (splited_lesson[0].Contains("1ч") && splited_lesson[1].Contains("2ч") && splited_lesson[2].Contains("2ч") && splited_lesson[3].Contains("1ч"))
                                        {
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[0] + '\n' + splited_lesson[3]}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n{splited_lesson[1] + '\n' + splited_lesson[2]}");
                                        }
                                    }
                                }
                                else
                                {
                                    lessonCount += 2;  // Учитываем пустую строку как урок (но не выводим её)
                                }
                            }

                            // Сохраняем расписание для группы
                            var groupSchedule = new Dictionary<string, List<string>>();
                            groupSchedule[groupName] = groupLessons;

                            // Добавляем расписание группы в общий список
                            schedule.Add(groupSchedule);
                        }
                    }
                }
            }

            return schedule;
        }


    }
}