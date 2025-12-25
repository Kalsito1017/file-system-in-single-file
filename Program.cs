using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace FileSystemContainer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.WriteLine("Файлова система върху контейнер");
            Console.WriteLine("===============================\n");

            string containerPath = "filesystem.fsc";
            FileSystemContainer fs = null;

            try
            {
                fs = new FileSystemContainer(containerPath);
                Console.WriteLine($"Контейнерът е зареден от: {containerPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Грешка при зареждане на контейнера: {ex.Message}");
                return;
            }

            if (args.Length > 0)
            {
                // Режим на командния ред
                ProcessCommandLineArgs(args, fs);
            }
            else
            {
                // Интерактивен режим
                RunInteractiveMode(fs);
            }
        }

        static void ProcessCommandLineArgs(string[] args, FileSystemContainer fs)
        {
            try
            {
                string command = args[0].ToLower();

                switch (command)
                {
                    case "cpin":
                        if (args.Length != 3)
                            throw new ArgumentException("Използване: cpin <външен_път> <вътрешно_име>");
                        fs.CopyIn(args[1], args[2]);
                        Console.WriteLine($"Файлът {args[2]} е копиран успешно.");
                        break;

                    case "ls":
                        var items = fs.ListContents();
                        Console.WriteLine("Съдържание на текущата директория:");
                        Console.WriteLine(new string('-', 60));
                        foreach (var item in items)
                        {
                            Console.WriteLine(item);
                        }
                        Console.WriteLine(new string('-', 60));
                        break;

                    case "rm":
                        if (args.Length != 2)
                            throw new ArgumentException("Използване: rm <име>");
                        Console.Write($"Сигурни ли сте, че искате да изтриете '{args[1]}'? (y/n): ");
                        string confirm = Console.ReadLine()?.Trim().ToLower();
                        if (confirm == "y" || confirm == "yes")
                        {
                            try
                            {
                                // Дайте време за освобождаване на ресурси
                                System.Threading.Thread.Sleep(50);
                                fs.Remove(args[1]);
                                Console.WriteLine($"✓ Файлът '{args[1]}' е изтрит.");
                            }
                            catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                            {
                                Console.WriteLine($"✗ Файлът е заключен. Опитайте отново след 2 секунди...");
                                System.Threading.Thread.Sleep(2000);
                                try
                                {
                                    fs.Remove(args[1]);
                                    Console.WriteLine($"✓ Файлът '{args[1]}' е изтрит (втори опит).");
                                }
                                catch (Exception ex2)
                                {
                                    Console.WriteLine($"✗ Грешка: {ex2.Message}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"✗ Грешка: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine("Операцията е отменена.");
                        }
                        break;

                    case "cpout":
                        if (args.Length != 3)
                            throw new ArgumentException("Използване: cpout <вътрешно_име> <външен_път>");
                        fs.CopyOut(args[1], args[2]);
                        Console.WriteLine($"Файлът {args[1]} е експортиран успешно.");
                        break;

                    case "md":
                        if (args.Length != 2)
                            throw new ArgumentException("Използване: md <име_на_директория>");
                        fs.CreateDirectory(args[1]);
                        Console.WriteLine($"Директория {args[1]} е създадена.");
                        break;

                    case "cd":
                        if (args.Length != 2)
                            throw new ArgumentException("Използване: cd <път>");
                        fs.ChangeDirectory(args[1]);
                        Console.WriteLine($"Текуща директория променена.");
                        break;

                    case "rd":
                        if (args.Length != 2)
                            throw new ArgumentException("Използване: rd <име_на_директория>");
                        fs.RemoveDirectory(args[1]);
                        Console.WriteLine($"Директория {args[1]} е изтрита.");
                        break;

                    case "defrag":
                        fs.Defragment();
                        Console.WriteLine("Дефрагментацията завърши успешно.");
                        break;

                    case "info":
                        Console.WriteLine(fs.GetContainerInfo());
                        break;

                    case "help":
                        ShowHelp();
                        break;

                    default:
                        Console.WriteLine($"Неизвестна команда: {command}");
                        ShowHelp();
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Грешка: {ex.Message}");
            }
        }

        static void RunInteractiveMode(FileSystemContainer fs)
        {
            ShowHelp();

            while (true)
            {
                Console.Write("\nfs> ");
                string input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input))
                    continue;

                if (input.ToLower() == "exit" || input.ToLower() == "quit")
                {
                    Console.WriteLine("Излизане от програмата...");
                    break;
                }

                string[] args = ParseCommandLine(input);

                try
                {
                    ProcessInteractiveCommand(args, fs);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Грешка: {ex.Message}");
                }
            }
        }

        static void ProcessInteractiveCommand(string[] args, FileSystemContainer fs)
        {
            if (args.Length == 0)
                return;

            string command = args[0].ToLower();

            switch (command)
            {
                case "cpin":
                    if (args.Length != 3)
                        throw new ArgumentException("Използване: cpin <външен_път> <вътрешно_име>");
                    fs.CopyIn(args[1], args[2]);
                    Console.WriteLine($"✓ Файлът {args[2]} е копиран успешно.");
                    break;

                case "ls":
                    var items = fs.ListContents();
                    if (items.Count == 0)
                    {
                        Console.WriteLine("Директорията е празна.");
                    }
                    else
                    {
                        Console.WriteLine("\nСъдържание на текущата директория:");
                        Console.WriteLine(new string('-', 70));
                        Console.WriteLine($"{"Име",-30} {"Тип",-5} {"Размер",10} {"Компресия",15}");
                        Console.WriteLine(new string('-', 70));
                        foreach (var item in items)
                        {
                            Console.WriteLine(item);
                        }
                        Console.WriteLine(new string('-', 70));
                    }
                    break;

                case "rm":
                    if (args.Length != 2)
                        throw new ArgumentException("Използване: rm <име>");
                    Console.Write($"Сигурни ли сте, че искате да изтриете {args[1]}? (y/n): ");
                    string confirm = Console.ReadLine()?.Trim().ToLower();
                    if (confirm == "y" || confirm == "yes")
                    {
                        fs.Remove(args[1]);
                        Console.WriteLine($"✓ Файлът {args[1]} е изтрит.");
                    }
                    else
                    {
                        Console.WriteLine("Операцията е отменена.");
                    }
                    break;

                case "cpout":
                    if (args.Length != 3)
                        throw new ArgumentException("Използване: cpout <вътрешно_име> <външен_път>");
                    fs.CopyOut(args[1], args[2]);
                    Console.WriteLine($"✓ Файлът {args[1]} е експортиран успешно.");
                    break;

                case "md":
                    if (args.Length != 2)
                        throw new ArgumentException("Използване: md <име_на_директория>");
                    fs.CreateDirectory(args[1]);
                    Console.WriteLine($"✓ Директория {args[1]} е създадена.");
                    break;

                case "cd":
                    if (args.Length != 2)
                        throw new ArgumentException("Използване: cd <път>");
                    fs.ChangeDirectory(args[1]);
                    Console.WriteLine($"✓ Текуща директория променена.");
                    break;

                case "rd":
                    if (args.Length != 2)
                        throw new ArgumentException("Използване: rd <име_на_директория>");
                    Console.Write($"Сигурни ли сте, че искате да изтриете директория {args[1]} и всичко в нея? (y/n): ");
                    confirm = Console.ReadLine()?.Trim().ToLower();
                    if (confirm == "y" || confirm == "yes")
                    {
                        fs.RemoveDirectory(args[1]);
                        Console.WriteLine($"✓ Директория {args[1]} е изтрита.");
                    }
                    else
                    {
                        Console.WriteLine("Операцията е отменена.");
                    }
                    break;

                case "defrag":
                    Console.WriteLine("Извършване на дефрагментация...");
                    fs.Defragment();
                    Console.WriteLine("✓ Дефрагментацията завърши успешно.");
                    break;

                case "info":
                    Console.WriteLine("\nИнформация за контейнера:");
                    Console.WriteLine(new string('-', 40));
                    Console.WriteLine(fs.GetContainerInfo());
                    Console.WriteLine(new string('-', 40));
                    break;

                case "help":
                    ShowHelp();
                    break;

                case "clear":
                    Console.Clear();
                    break;

                default:
                    Console.WriteLine($"Неизвестна команда: {command}");
                    Console.WriteLine("Въведете 'help' за списък с команди.");
                    break;
            }
        }

        static string[] ParseCommandLine(string input)
        {
            List<string> args = new List<string>();
            string currentArg = "";
            bool inQuotes = false;

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ' ' && !inQuotes)
                {
                    if (!string.IsNullOrEmpty(currentArg))
                    {
                        args.Add(currentArg);
                        currentArg = "";
                    }
                }
                else
                {
                    currentArg += c;
                }
            }

            if (!string.IsNullOrEmpty(currentArg))
            {
                args.Add(currentArg);
            }

            return args.ToArray();
        }

        static void ShowHelp()
        {
            Console.WriteLine("\nДостъпни команди:");
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("  cpin <външен_файл> <вътрешно_име> - Копира файл в контейнера");
            Console.WriteLine("  cpout <вътрешно_име> <външен_файл> - Копира файл от контейнера");
            Console.WriteLine("  ls                                - Показва съдържанието");
            Console.WriteLine("  rm <име>                          - Изтрива файл");
            Console.WriteLine("  md <име>                          - Създава директория");
            Console.WriteLine("  cd <път>                          - Променя текущата директория");
            Console.WriteLine("  rd <име>                          - Изтрива директория");
            Console.WriteLine("  defrag                            - Дефрагментира контейнера");
            Console.WriteLine("  info                              - Информация за контейнера");
            Console.WriteLine("  help                              - Показва този помощен текст");
            Console.WriteLine("  clear                             - Изчиства екрана");
            Console.WriteLine("  exit / quit                       - Излиза от програмата");
            Console.WriteLine(new string('-', 50));
            Console.WriteLine("Примери:");
            Console.WriteLine("  cpin C:\\file.txt myfile.txt");
            Console.WriteLine("  cpout myfile.txt D:\\backup.txt");
            Console.WriteLine("  md documents");
            Console.WriteLine("  cd documents");
            Console.WriteLine("  cd ..");
            Console.WriteLine("  cd \\");
            Console.WriteLine(new string('-', 50));
        }
    }
}