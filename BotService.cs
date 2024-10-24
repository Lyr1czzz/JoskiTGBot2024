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
        private bool fileRole = true;

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
            try
            {
                if (update.Message != null)
                {
                    if (!await RedisService.IsStringExist(update.Message.Chat.Id.ToString()))
                    {
                        await RedisService.SetValue(update.Message.Chat.Id.ToString(), "1", TimeSpan.FromSeconds(5));

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
                            await _botClient.SendTextMessageAsync(message.Chat.Id, "Инструкция по использованию: \nЕсли Вы хотите получать ваше расписание, выберите в меню пункт \"Выбор роли\" и укажите вашу роль как преподавателя или учащегося.\nДалее нужно написать название своей группы в корректной форме, указанной ботом!\nПосле этого вам будет приходить расписание АВТОМАТИЧЕСКИ, как только оно будет составленно.\nУбедительная просьба не баловаться!");
                        }
                        else if (command == "/setrole")
                        {
                            await ShowUserMenu(message.Chat.Id);
                        }
                        else if (command == "/getschedule")
                        {
                            await SendScheduleForChanged(message.Chat.Id);
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
                                await _botClient.SendTextMessageAsync(message.Chat.Id, "Изменения приняты.");
                            }
                            else if (user.Role == "Преподаватель" && Regex.IsMatch(command, @"^[А-ЯЁ][а-яё]+ [А-ЯЁ]\.[А-ЯЁ]\.$"))
                            {
                                await ChangeUserGroupOrFIO(user.TelegramUserId, command);
                                await _botClient.SendTextMessageAsync(message.Chat.Id, "Изменения приняты.");
                            }
                            else
                            {
                                await _botClient.SendTextMessageAsync(message.Chat.Id, "Вы написали какую-то бяку. Попробуйте еще раз");
                            }
                            return;
                        }
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(update.Message.Chat.Id, "Пожалуйста, не балуйся. Одно сообщение в 5 секунд!");
                    }
                }

                if (update.CallbackQuery != null)
                {
                    if (!await RedisService.IsStringExist(update.CallbackQuery.Id.ToString()))
                    {
                        await RedisService.SetValue(update.CallbackQuery.Id.ToString(), "1", TimeSpan.FromSeconds(5));

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
                    else
                    {
                        await _botClient.SendTextMessageAsync(update.CallbackQuery.Id, "Пожалуйста, не балуйся. Одно сообщение в 5 секунд!");
                    }
                }
            }
            catch (Telegram.Bot.Exceptions.ApiRequestException ex)
            {
                if (ex.Message.Contains("Forbidden: bot was blocked by the user"))
                {
                    if (update.Message is not null)
                    {
                        Console.WriteLine($"Пользователь {update.Message?.Chat.Id.ToString() ?? "NET"} заблокировал бота. Удаляем пользователя из базы данных.");

                        // Удаление пользователя из базы данных
                        var delet_user = _dbContext.Users.FirstOrDefault(u => u.TelegramUserId == update.Message!.Chat.Id);
                        if (delet_user is not null)
                        {
                            _dbContext.Users.Remove(delet_user!);
                            await _dbContext.SaveChangesAsync();
                        }
                    }
                }
                else
                {
                    // Обработка других ошибок
                    Console.WriteLine($"Произошла ошибка: {ex.Message}");
                }
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


            if (fileRole)
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string fileName = "scheduleTech.bin"; // имя вашего файла
                string fullPath = Path.Combine(basePath, "Data", fileName);
                SaveToBinaryFile(schedule, fullPath);
            }
            else
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string fileName = "scheduleStud.bin"; // имя вашего файла
                string fullPath = Path.Combine(basePath, "Data", fileName);
                SaveToBinaryFile(schedule, fullPath);
            }
            await SendScheduleForEveryone();
            var scheduleService = new ScheduleService(schedule);
            stopwatch.Stop();
            Console.WriteLine("Сохранил и разослал за" + stopwatch.Elapsed.ToString());
        }

        private async Task SendScheduleForChanged(long chatId)
        {
            var user = _dbContext.Users.Where(u => u.TelegramUserId == chatId).FirstOrDefault();

            var schedule = new List<Dictionary<string, List<string>>>();

            if (user != null)
            {
                if (user.Role == "Учащийся")
                {
                    string basePath = AppDomain.CurrentDomain.BaseDirectory;
                    string fileName = "scheduleStud.bin"; // имя вашего файла
                    string fullPath = Path.Combine(basePath, "Data", fileName);
                    schedule = LoadFromBinaryFile(fullPath);
                }
                else if (user.Role == "Преподаватель")
                {
                    string basePath = AppDomain.CurrentDomain.BaseDirectory;
                    string fileName = "scheduleTech.bin"; // имя вашего файла
                    string fullPath = Path.Combine(basePath, "Data", fileName);
                    schedule = LoadFromBinaryFile(fullPath);
                }

                var scheduleService = new ScheduleService(schedule);

                var scheduleMessage = scheduleService.GetScheduleForChanged(user.GroupName);

                await _botClient.SendTextMessageAsync(user.TelegramUserId, scheduleMessage, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
            }
            else
            {
                Console.WriteLine("Пользователь не найден");
            }
        }
        private async Task SendScheduleForEveryone()
        {
            var schedule = new List<Dictionary<string, List<string>>>();

            if (fileRole)
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string fileName = "scheduleTech.bin"; // имя вашего файла
                string fullPath = Path.Combine(basePath, "Data", fileName);
                schedule = LoadFromBinaryFile(fullPath);
            }
            else
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string fileName = "scheduleStud.bin"; // имя вашего файла
                string fullPath = Path.Combine(basePath, "Data", fileName);
                schedule = LoadFromBinaryFile(fullPath);
            }

            var scheduleService = new ScheduleService(schedule);


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
                            if (delet_user is not null)
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
        }

        // Сохранение данных в бинарный файл
        public void SaveToBinaryFile(List<Dictionary<string, List<string>>> data, string filePath)
        {
            using (var writer = new BinaryWriter(System.IO.File.Open(filePath, FileMode.Create)))
            {
                // Записываем количество словарей
                writer.Write(data.Count);

                foreach (var dictionary in data)
                {
                    // Записываем количество ключей в словаре
                    writer.Write(dictionary.Count);

                    foreach (var pair in dictionary)
                    {
                        // Записываем ключ
                        writer.Write(pair.Key);

                        // Записываем количество элементов в списке
                        writer.Write(pair.Value.Count);

                        foreach (var value in pair.Value)
                        {
                            // Записываем каждый элемент списка
                            writer.Write(value);
                        }
                    }
                }
            }
        }

        // Загрузка данных из бинарного файла
        public List<Dictionary<string, List<string>>> LoadFromBinaryFile(string filePath)
        {
            var data = new List<Dictionary<string, List<string>>>();

            using (var reader = new BinaryReader(System.IO.File.Open(filePath, FileMode.Open)))
            {
                // Читаем количество словарей
                int dictionaryCount = reader.ReadInt32();

                for (int i = 0; i < dictionaryCount; i++)
                {
                    var dictionary = new Dictionary<string, List<string>>();

                    // Читаем количество ключей в словаре
                    int keyCount = reader.ReadInt32();

                    for (int j = 0; j < keyCount; j++)
                    {
                        // Читаем ключ
                        string key = reader.ReadString();

                        // Читаем количество элементов в списке
                        int listCount = reader.ReadInt32();
                        var list = new List<string>();

                        for (int k = 0; k < listCount; k++)
                        {
                            // Читаем каждый элемент списка
                            list.Add(reader.ReadString());
                        }

                        // Добавляем в словарь
                        dictionary[key] = list;
                    }

                    data.Add(dictionary);
                }
            }

            return data;
        }

        private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"Ошибка: {exception.Message + '\n' + exception.InnerException}");

            return Task.CompletedTask;
        }
    }
}