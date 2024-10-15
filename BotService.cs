using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using JoskiTGBot2024.Database;
using JoskiTGBot2024.Models;
using JoskiTGBot2024.Services;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using Telegram.Bot.Types.ReplyMarkups;
using UserModel = JoskiTGBot2024.Models.User;
using System.Text.RegularExpressions;

namespace JoskiTGBot2024
{
    public class BotService
    {
        private readonly TelegramBotClient _botClient;
        private readonly ApplicationDbContext _dbContext;
        private readonly ExcelService _excelService;
        public bool fileRole;
        string changeGroupID = "";

        public BotService(string token)
        {
            _botClient = new TelegramBotClient(token);
            _dbContext = new ApplicationDbContext();
            _excelService = new ExcelService();
        }

        public void Start()
        {
            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { } // Получаем все типы обновлений
            };

            _botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions);
            Console.WriteLine("Бот попущен. Нажмите Enter для завершения.");
            Console.ReadLine();
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            if (update.Message != null)
            {
                var message = update.Message;
                var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == message.Chat.Id);
                var command = message.Text;
                bool answer = true;

                switch (command)
                {
                    case "/start":
                        if (user == null)
                        {
                            await AskForRole(message.Chat.Id); // Спрашиваем роль
                        }
                        else if (user.IsAdmin)
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, $"Вы зарегистрированы как администратор.");
                            await ShowAdminMenu(message.Chat.Id);
                        }
                        else
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, $"Вы зарегистрированы как {user.Role}.");
                        }
                        break;
                    case "Учащийся":
                        if (user == null)
                        {
                            await RegisterUser(message.Chat.Id, command);
                        }
                        else
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, $"Вы не можете выбрать роль сейчас");
                        }
                        break;
                    case "Преподаватель":
                        if (user == null)
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, $"Введите пароль.");
                        }
                        break;
                    case "Joski_Aa1111!":
                        if (user == null && user.Role != "Учащийся") { 
                            await RegisterUser(message.Chat.Id, "Преподаватель");
                        }
                        break;
                    case "📚 Отправить расписание для студентов":
                        if (IsAdmin(message.From.Id))
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, "Пожалуйста, отправьте файл Excel с расписанием для студентов.");
                            fileRole = false; //мы получим расписание студентов
                        }
                        else
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, "Для этого действия нужно обладать правами администратора.");
                        }
                        break;
                    case "👨‍🏫 Отправить расписание для преподавателей":
                        if (IsAdmin(message.From.Id))
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, "Пожалуйста, отправьте файл Excel с расписанием для преподавателей.");
                            fileRole = true; //мы получим расписание преподов
                        }
                        else
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, "Для этого действия нужно обладать правами администратора.");
                        }
                        break;

                    case "Жоско сменить группу":
                        if (user.Role == "Учащийся")
                        {
                            changeGroupID = message.Chat.Id.ToString();
                            await _botClient.SendTextMessageAsync(message.Chat.Id, $"Введите новую группу");
                            
                        }
                        break;
                    default:
                        answer = false;
                        break;
                }

                if (user != null)
                {
                    if (message.Document != null && IsAdmin(message.From.Id))
                    {
                        try
                        {
                            await ProcessAdminFile(message.Chat.Id, message.Document.FileId);
                            string successMessage = fileRole ? "преподавателей" : "учащихся";
                            await _botClient.SendTextMessageAsync(message.Chat.Id, $"Расписание для {successMessage} принято на обработку.");
                        }
                        catch (Exception ex)
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, $"Произошла ошибка при обработке файла: {ex.Message}. Пожалуйста, проверьте формат файла.");
                        }
                        answer = true;
                    }

                    if (command != null && Regex.IsMatch(command, @"^[А-ЯЁ][а-яё]+ [А-ЯЁ]\.[А-ЯЁ]\.$"))
                    {
                        if (user.Role == "Преподаватель")
                        {
                            await ChangeUserGroup(message.Chat.Id, command); //выбор фамилии препода
                        }
                        else
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, $"Вы не можете поменять ФИО сейчас");
                        }
                        answer = true;
                    }

                    if (command != null && Regex.IsMatch(command, @"^[А-ЯЁ]{1,2}-\d{4}$") && (string.IsNullOrEmpty(user.GroupName) || message.Chat.Id.ToString() == changeGroupID))
                    {
                        await ChangeUserGroup(message.Chat.Id, command); //выбор группы студента
                        answer = true;
                    }

                    if (!answer)
                    {
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "Вы написали какую-то бяку. Попробуйте что-то другое.");
                    }
                }
            }
        }

        // Метод для запроса роли
        private async Task AskForRole(long chatId)
        {
            var roleButtons = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton("Учащийся"),
                new KeyboardButton("Преподаватель")
            })
            {
                ResizeKeyboard = true,
                OneTimeKeyboard = true // Скрыть клавиатуру после выбора
            };

            await _botClient.SendTextMessageAsync(chatId, "Пожалуйста, выберите вашу роль:", replyMarkup: roleButtons);
        }

        // Метод для регистрации пользователя
        private async Task RegisterUser(long chatId, string role)
        {
            var newUser = new UserModel { TelegramUserId = chatId, Role = role, IsAdmin = false };
            _dbContext.Users.Add(newUser);
            await _dbContext.SaveChangesAsync();

            if (role == "Учащийся")
            {
                await _botClient.SendTextMessageAsync(chatId, "Пожалуйста, введите вашу группу. ");
                await _botClient.SendTextMessageAsync(chatId, $"Внимание! вы НЕ сможете сменить группу после ее выбора. Пожалуйста, постарайтесь вспомнить ТОЧНОЕ название своей группы. Форма записи: БД-2016.");

            }
            else if (role == "Преподаватель")
            {
                await _botClient.SendTextMessageAsync(chatId, "Пожалуйста, введите ваше ФИО в формате Фамилия И.О.");
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, "Произошла ошибка, попробуйте снова.");
            }
        }

        // Метод для изменения группы пользователя
        private async Task ChangeUserGroup(long chatId, string newGroupName)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == chatId);
            if (user != null)
            {
                user.GroupName = newGroupName;
                await _dbContext.SaveChangesAsync();
                await _botClient.SendTextMessageAsync(chatId, $"Измененно на {newGroupName}.");
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, "Произошла ошибка, попробуйте снова.");
            }
        }

        // Метод для обработки загрузки файлов от администраторов
        private async Task ProcessAdminFile(long adminId, string fileId)
        {
            var file = await _botClient.GetFileAsync(fileId);
            var fileStream = new MemoryStream();
            await _botClient.DownloadFileAsync(file.FilePath, fileStream);

            var schedule = _excelService.ProcessExcelFile(fileStream);
            var scheduleService = new ScheduleService(schedule);

            if (fileRole)
            {
                var teachers = _dbContext.Users.Where(u => u.Role == "Преподаватель").ToList();

                foreach (var user in teachers)
                {
                    if (user.GroupName.Contains("Лейко Д.А."))
                    {
                        var scheduleMessage = scheduleService.GetScheduleForGroup(user.GroupName);
                        await _botClient.SendTextMessageAsync(user.TelegramUserId, "Спец заказ для МС микроба газонюха\n" + scheduleMessage, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                    }
                    else if (!user.IsAdmin)
                    {
                        var scheduleMessage = scheduleService.GetScheduleForGroup(user.GroupName);
                        await _botClient.SendTextMessageAsync(user.TelegramUserId, scheduleMessage, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                    }
                }
            }
            else
            {
                var students = _dbContext.Users.Where(u => u.Role == "Учащийся").ToList();

                foreach (var user in students)
                {
                    if (!user.IsAdmin)
                    {
                        var scheduleMessage = scheduleService.GetScheduleForGroup(user.GroupName);
                        await _botClient.SendTextMessageAsync(user.TelegramUserId, scheduleMessage, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                    }
                }
            }
        }

        // Метод для отображения меню администратора
        private async Task ShowAdminMenu(long chatId)
        {
            var adminButtons = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton("📚 Отправить расписание для студентов"),
                new KeyboardButton("👨‍🏫 Отправить расписание для преподавателей")
            })
            {
                ResizeKeyboard = true
            };

            await _botClient.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: adminButtons);
        }

        private bool IsAdmin(long userId)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == userId);
            return user != null && user.IsAdmin;
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
