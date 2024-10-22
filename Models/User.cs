using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JoskiTGBot2024.Models
{
    public class User
    {
        public int Id { get; set; }
        public long TelegramUserId { get; set; }
        public string? GroupName { get; set; } = string.Empty!;
        public string? Role { get; set; } = string.Empty!;
        public string? Name { get; set; } = string.Empty!;
        public bool IsAdmin { get; set; }
    }
}
