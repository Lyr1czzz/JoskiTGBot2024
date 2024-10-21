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

                formattedSchedule.AppendLine($"📅 *Расписание для {groupName}:*");

                formattedSchedule.AppendLine();
                foreach (var lesson in lessons)
                {
                    formattedSchedule.AppendLine(lesson);       
                    formattedSchedule.AppendLine();
                }

                return formattedSchedule.ToString();
            }
            else
            {
                return $"Расписание для группы {groupName} не найдено.";
            }
        }
    }
}
