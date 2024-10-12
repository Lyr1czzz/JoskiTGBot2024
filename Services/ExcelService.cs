using OfficeOpenXml;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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

                        // Проверяем, является ли ячейка названием группы
                        if (!string.IsNullOrEmpty(cellValue) && cellValue.Contains("-2"))
                        {
                            string groupName = cellValue;
                            var groupLessons = new List<string>();

                            int lessonCount = 0;  // Считаем уроки

                            // Читаем следующие строки с уроками
                            for (int nextRow = row + 1; nextRow <= row + 7 && nextRow <= rows; nextRow++)  // Читаем 14 строк (по два урока на строку, если нет 1ч или 2ч)
                            {
                                string lesson = worksheet.Cells[nextRow, col].Text;
                                string para = worksheet.Cells[nextRow, 2].Text;

                                string next_lesson = worksheet.Cells[nextRow + 1, col].Text;

                                if (!string.IsNullOrEmpty(lesson))
                                {
                                    if ((lesson.Contains("2ч") && para == "1 пара") && (!next_lesson.Contains("1ч") && para == "1 пара"))
                                    {
                                        lessonCount++;  // Урок считается за один
                                        groupLessons.Add($"Окошко");
                                        lessonCount++;  // Урок считается за один
                                        groupLessons.Add($"Урок {lessonCount}:\n {lesson}");
                                    }
                                    
                                    else
                                    {
                                        // Проверяем, есть ли подгруппы (1п или 2п)
                                        if (lesson.Contains("(1п)") || lesson.Contains("(2п)"))
                                        {
                                            lessonCount++;  // Считается как отдельный урок для подгруппы
                                            groupLessons.Add($"Урок {lessonCount}:\n {lesson}");

                                            // Для второй подгруппы также считаем как отдельный урок
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n {lesson}");
                                        }
                                        else
                                        {
                                            // Если нет 1ч, 2ч или подгрупп, то урок считается за два урока
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n {lesson}");
                                            lessonCount++;
                                            groupLessons.Add($"Урок {lessonCount}:\n {lesson}");
                                        }
                                    }
                                }
                                else
                                {
                                    lessonCount+=2;  // Учитываем пустую строку как урок (но не выводим её)
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
