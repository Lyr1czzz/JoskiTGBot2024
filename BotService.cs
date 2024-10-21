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
using System.Diagnostics;

namespace JoskiTGBot2024
{
    public class BotService
    {
        private readonly TelegramBotClient _botClient;
        private readonly ApplicationDbContext _dbContext;
        private readonly ExcelService _excelService;
        private bool fileRole;
        private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

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
                AllowedUpdates = { }
            };

            _botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions);
            Console.WriteLine("Бот попущен. Нажмите Enter для завершения.");
            Console.ReadLine();
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            await semaphore.WaitAsync();
            try
            {
                if (update.Message != null)
                {
                    var message = update.Message;
                    var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == message.Chat.Id);
                    var command = message.Text != null ? message.Text : string.Empty;

                    if (command == "/start")
                    {
                        if (user != null && !user.IsAdmin)
                        {
                            _dbContext.Users.Remove(user);
                            await _dbContext.SaveChangesAsync();
                            await RegisterEmptyUser(message.Chat.Id, message.Chat.Username);
                            await _dbContext.SaveChangesAsync();
                        }
                        else if (user == null)
                        {
                            await RegisterEmptyUser(message.Chat.Id, message.Chat.Username);
                            await _dbContext.SaveChangesAsync();
                        }

                        if (IsAdmin(message.From.Id))
                        {
                            await ShowAdminMenu(message.Chat.Id);
                        }
                        else
                        {
                            await ShowUserMenu(message.Chat.Id);
                        }
                    }
                    else if (command == "/help")
                    {
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "ЭЩКЕРЕ");

                    }
                    else if (command == "📚 для студентов")
                    {
                        fileRole = false;
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "Пожалуйста, отправьте файл Excel с расписанием для студентов.");
                    }
                    else if (command == "👨‍🏫 для преподавателей")
                    {
                        fileRole = true;
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "Пожалуйста, отправьте файл Excel с расписанием для преподавателей.");
                    }
                    else if (message.Document != null && user != null)
                    {
                        if (user?.IsAdmin == true)
                        {
                            await ProcessAdminFile(user.TelegramUserId, message.Document.FileId);
                        }
                    }
                    else if (user != null)
                    {
                        if (user.Role == "Учащийся" && Regex.IsMatch(command, @"^[А-ЯЁ]{1,2}-\d{4}$"))
                        {
                            await ChangeUserGroupOrFIO(user.TelegramUserId, command);
                            await ShowUserMenu(message.Chat.Id); // Возвращаемся к пользовательскому меню
                            await _botClient.SendTextMessageAsync(message.Chat.Id, "Изменения приняты.");
                        }
                        else if (user.Role == "Преподаватель" && Regex.IsMatch(command, @"^[А-ЯЁ][а-яё]+ [А-ЯЁ]\.[А-ЯЁ]\.$"))
                        {
                            await ChangeUserGroupOrFIO(user.TelegramUserId, command);
                            await ShowUserMenu(message.Chat.Id); // Возвращаемся к пользовательскому меню
                            await _botClient.SendTextMessageAsync(message.Chat.Id, "Изменения приняты.");
                        }
                        else
                        {
                            await _botClient.SendTextMessageAsync(message.Chat.Id, "Вы написали какую-то бяку. Попробуйте еще раз");
                        }
                        return;
                    }

                }



                if (update.CallbackQuery != null)
                {
                    var callbackQuery = update.CallbackQuery;

                    // Проверка на истечение времени действия callbackQuery
                    if (callbackQuery.Message == null || callbackQuery.Data == null)
                    {
                        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Действие не может быть выполнено, запрос устарел.", showAlert: true);
                        return;
                    }

                    var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == callbackQuery.Message.Chat.Id);

                    switch (callbackQuery.Data)
                    {
                        case "change_role":
                            await AskForRole(callbackQuery.Message.Chat.Id);
                            break;

                        case "role_student":
                            await ChangeUserRole(callbackQuery.Message.Chat.Id, "Учащийся");
                            await _botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Роль 'Учащийся' установлена. Пожалуйста, введите вашу группу. Пример: П-2109");
                            break;

                        case "role_teacher":
                            await ChangeUserRole(callbackQuery.Message.Chat.Id, "Преподаватель");
                            await _botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "Роль 'Преподаватель' установлена. Пожалуйста, введите ваше ФИО. Пример: Фамилия И.О.");
                            break;
                        default:
                            await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, "Каво? выберите действие из меню.");
                            break;
                    }
                }
               await Task.Delay(2000);
            }
            finally
            {
                semaphore.Release();
            }
            
        }

        // Метод для отображения меню для обычного пользователя
        private async Task ShowUserMenu(long chatId)
        {
            var userMenu = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Сменить роль", "change_role")
                }
            });

            await _botClient.SendTextMessageAsync(chatId, "Меню действий:", replyMarkup: userMenu);
        }

        // Метод для отображения меню для администратора
        private async Task ShowAdminMenu(long chatId)
        {
            var adminMenu = new ReplyKeyboardMarkup(new[]
            {
                new[] // Первый ряд кнопок
                {
                    new KeyboardButton("📚 для студентов"),
                    new KeyboardButton("👨‍🏫 для преподавателей")
                }
            })
            {
                ResizeKeyboard = true, // Подгонка под экран устройства
                OneTimeKeyboard = false // Клавиатура будет видна постоянно, пока не отправлена новая клавиатура
            };

            await _botClient.SendTextMessageAsync(chatId, "Меню администратора:", replyMarkup: adminMenu);
        }


        // Метод для запроса изменения группы или ФИО
        private async Task ChangeUserGroupOrFIO(long chatId, string newValue)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == chatId);
            if (user != null)
            {
                user.GroupName = newValue;
                await _dbContext.SaveChangesAsync();
            }
        }

        // Метод для запроса выбора роли
        private async Task AskForRole(long chatId)
        {
            var roleButtons = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithCallbackData("Учащийся", "role_student"),
                InlineKeyboardButton.WithCallbackData("Преподаватель", "role_teacher")
            });

            await _botClient.SendTextMessageAsync(chatId, "Пожалуйста, выберите вашу роль:", replyMarkup: roleButtons);
        }

        // Метод для смены роли пользователя
        private async Task ChangeUserRole(long chatId, string role)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == chatId);
            if (user != null)
            {
                user.Role = role;
                user.GroupName = ""; // Сбрасываем группу/ФИО при смене роли
                await _dbContext.SaveChangesAsync();
            }
        }

        // Метод для регистрации нового пользователя с пустыми полями, кроме ChatId
        private async Task RegisterEmptyUser(long chatId, string name)
        {
            var newUser = new UserModel
            {
                TelegramUserId = chatId,
                Role = "", // роль еще не выбрана
                Name = name,
                GroupName = "" // пустая группа/ФИО
            };

            _dbContext.Users.Add(newUser);
            await _dbContext.SaveChangesAsync();
        }

        private bool IsAdmin(long userId)
        {
            var user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == userId);
            return user != null && user.IsAdmin;
        }

        private async Task ProcessAdminFile(long adminId, string fileId)
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            var file = await _botClient.GetFileAsync(fileId);
            var fileStream = new MemoryStream();
            await _botClient.DownloadFileAsync(file.FilePath, fileStream);

            var schedule = _excelService.ProcessExcelFile(fileStream);
            var scheduleService = new ScheduleService(schedule);
            stopwatch.Stop();
            Console.WriteLine(stopwatch.Elapsed.ToString());
            stopwatch.Restart();

            if (fileRole)
            {
                var teachers = _dbContext.Users.Where(u => u.Role == "Преподаватель").ToList();

                foreach (var user in teachers)
                {
                    var scheduleMessage = scheduleService.GetScheduleForGroup(user.GroupName);
                    await _botClient.SendTextMessageAsync(user.TelegramUserId, scheduleMessage, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                }

            }
            else
            {
                var students = _dbContext.Users.Where(u => u.Role == "Учащийся").ToList();

                foreach (var user in students)
                {
                    var scheduleMessage = scheduleService.GetScheduleForGroup(user.GroupName);
                    var chatId = user.TelegramUserId;
                    try
                    {
                        await _botClient.SendTextMessageAsync(user.TelegramUserId, scheduleMessage, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
                    }
                    catch (Telegram.Bot.Exceptions.ApiRequestException ex)
                    {
                        if (ex.Message.Contains("Forbidden: bot was blocked by the user"))
                        {
                            Console.WriteLine($"Пользователь {chatId} заблокировал бота. Удаляем пользователя из базы данных.");

                            // Удаление пользователя из базы данных
                            var delet_user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == chatId);
                            if (user != null)
                            {
                                _dbContext.Users.Remove(user);
                                await _dbContext.SaveChangesAsync();
                            }
                        }
                        else
                        {
                            // Обработка других ошибок
                            Console.WriteLine($"Произошла ошибка: {ex.Message}");
                        }
                    }
                }
            }
            stopwatch.Stop();
            Console.WriteLine(stopwatch.Elapsed.ToString());
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка: {exception.Message}");
            return Task.CompletedTask;
        }
    }
}
