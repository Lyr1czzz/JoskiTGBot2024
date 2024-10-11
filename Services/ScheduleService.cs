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
                return string.Join("\n", scheduleForGroup[groupName]);
            }
            return "Расписание не найдено.";
        }
    }
}
