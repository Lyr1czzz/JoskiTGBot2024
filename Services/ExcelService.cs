using OfficeOpenXml;
using System.IO;
using System.Collections.Generic;

namespace JoskiTGBot2024.Services
{
    public class ExcelService
    {
        public List<Dictionary<string, List<string>>> ProcessExcelFile(Stream fileStream)
        {
            // Устанавливаем контекст лицензии для EPPlus
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var schedule = new List<Dictionary<string, List<string>>>();

            using (var package = new ExcelPackage(fileStream))
            {
                ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
                int rows = worksheet.Dimension.Rows;
                int cols = worksheet.Dimension.Columns;

                for (int row = 2; row <= rows; row++)
                {
                    var rowData = new Dictionary<string, List<string>>();
                    string date = worksheet.Cells[row, 1].Text;
                    string lessonOrder = worksheet.Cells[row, 2].Text;

                    for (int col = 3; col <= cols; col += 8)
                    {
                        string groupName = worksheet.Cells[1, col].Text;
                        var groupLessons = new List<string>();

                        for (int lessonIndex = 0; lessonIndex < 7; lessonIndex++)
                        {
                            groupLessons.Add(worksheet.Cells[row, col + lessonIndex].Text);
                        }

                        rowData[groupName] = groupLessons;
                    }

                    schedule.Add(rowData);
                }
            }
            return schedule;
        }
    }
}
