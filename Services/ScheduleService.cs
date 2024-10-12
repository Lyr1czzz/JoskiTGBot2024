using System.Collections.Generic;
using System.Linq;

namespace JoskiTGBot2024.Services
{
    public class ScheduleService
    {
        private List<Dictionary<string, List<string>>> _schedule;

        public ScheduleService(List<Dictionary<string, List<string>>> schedule)
        {
            _schedule = schedule;
        }

        public string GetScheduleForGroup(string groupName)
        {
            var scheduleForGroup = _schedule.FirstOrDefault(s => s.ContainsKey(groupName));

            if (scheduleForGroup != null)
            {
                var lessons = scheduleForGroup[groupName];
                var formattedSchedule = new System.Text.StringBuilder();

                formattedSchedule.AppendLine($"📅 *Расписание для группы {groupName}:*");

                formattedSchedule.AppendLine();
                foreach (var lesson in lessons)
                {
                    formattedSchedule.AppendLine(lesson);
                    formattedSchedule.AppendLine();

                }
                //int lessonNumber = 1;  // Счетчик уроков
                //bool firstLessonFound = false;  // Флаг для отслеживания первого непустого урока

                //foreach (var lesson in lessons)
                //{
                //    // Пропускаем строки с названиями других групп
                //    if (lesson.Contains("-"))
                //    {
                //        continue;
                //    }

                //    // Проверяем, если урок не пустой
                //    if (!string.IsNullOrWhiteSpace(lesson))
                //    {
                //        // Если это первый найденный непустой урок
                //        if (!firstLessonFound)
                //        {
                //            firstLessonFound = true;

                //            // Устанавливаем правильную начальную нумерацию, начиная с текущего урока
                //            lessonNumber = lessons.IndexOf(lesson) + 1;
                //        }

                //        // Добавляем номер урока и смайлик
                //        formattedSchedule.AppendLine($"📝 *Урок {lessonNumber++}:*");

                //        // Убираем дублирование "Урок X:" в описании урока
                //        var cleanedLesson = lesson.StartsWith("Урок") ? lesson.Substring(lesson.IndexOf(":") + 1).Trim() : lesson;
                //        formattedSchedule.AppendLine(cleanedLesson);
                //        formattedSchedule.AppendLine();  // Переход на новую строку для разделения уроков
                //    }
                //    else if (firstLessonFound)
                //    {
                //        // Если урок пустой, но уже найден первый урок, увеличиваем счетчик номера урока
                //        lessonNumber++;
                //    }
                //}

                return formattedSchedule.ToString();
            }
            else
            {
                return $"Расписание для группы {groupName} не найдено.";
            }
        }
    }





}
