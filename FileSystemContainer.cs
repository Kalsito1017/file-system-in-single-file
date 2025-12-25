using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace FileSystemContainer
{
    // Структура за метаданни
    public struct MetadataEntry
    {
        public const int MAX_NAME_LENGTH = 248; // Максимална дължина на името

        public uint id;
        public uint parentId;
        public uint startOffset;
        public uint size;
        public uint nextBlock;
        public bool isDirectory;
        public bool isDeleted;
        public uint checksum;
        public uint compressedSize;
        public DateTime created;
        public DateTime modified;
        public char[] name;

        public static readonly int Size = 4 + 4 + 4 + 4 + 4 + 1 + 1 + 4 + 4 + 8 + 8 + (MAX_NAME_LENGTH * 2);

        public void Write(BinaryWriter writer)
        {
            writer.Write(id);
            writer.Write(parentId);
            writer.Write(startOffset);
            writer.Write(size);
            writer.Write(nextBlock);
            writer.Write(isDirectory);
            writer.Write(isDeleted);
            writer.Write(checksum);
            writer.Write(compressedSize);
            writer.Write(created.Ticks);
            writer.Write(modified.Ticks);

            for (int i = 0; i < MAX_NAME_LENGTH; i++)
            {
                if (i < name.Length)
                    writer.Write(name[i]);
                else
                    writer.Write('\0');
            }
        }

        public static MetadataEntry Read(BinaryReader reader)
        {
            MetadataEntry entry = new MetadataEntry();

            entry.id = reader.ReadUInt32();
            entry.parentId = reader.ReadUInt32();
            entry.startOffset = reader.ReadUInt32();
            entry.size = reader.ReadUInt32();
            entry.nextBlock = reader.ReadUInt32();
            entry.isDirectory = reader.ReadBoolean();
            entry.isDeleted = reader.ReadBoolean();
            entry.checksum = reader.ReadUInt32();
            entry.compressedSize = reader.ReadUInt32();
            entry.created = new DateTime(reader.ReadInt64());
            entry.modified = new DateTime(reader.ReadInt64());

            entry.name = new char[MAX_NAME_LENGTH];
            for (int i = 0; i < MAX_NAME_LENGTH; i++)
            {
                entry.name[i] = reader.ReadChar();
            }

            return entry;
        }

        public string GetName()
        {
            int nullIndex = Array.IndexOf(name, '\0');
            if (nullIndex >= 0)
            {
                return new string(name, 0, nullIndex);
            }
            return new string(name);
        }

        public void SetName(string name)
        {
            if (name.Length > MAX_NAME_LENGTH)
            {
                name = name.Substring(0, MAX_NAME_LENGTH);
            }

            this.name = new char[MAX_NAME_LENGTH];
            for (int i = 0; i < name.Length; i++)
            {
                this.name[i] = name[i];
            }
            for (int i = name.Length; i < MAX_NAME_LENGTH; i++)
            {
                this.name[i] = '\0';
            }
        }
    }

    public class FileSystemContainer
    {
        private const string MAGIC_STRING = "FSCONTAINER";
        private const uint CURRENT_VERSION = 1;
        private const uint METADATA_START = 2048; // Метаданните започват след 2KB

        private string containerPath;
        private uint currentDirectoryId;
        private uint nextId = 1;
        private BlockManager blockManager;

        public FileSystemContainer(string containerPath)
        {
            this.containerPath = containerPath;

            // Ако файлът не съществува, създайте го
            if (!File.Exists(containerPath))
            {
                Console.WriteLine($"Създаване на нов контейнер: {containerPath}");
                CreateNewContainer();
            }
            else
            {
                // Опитайте да заредите съществуващия
                try
                {
                    LoadExistingContainer();
                    Console.WriteLine($"Зареден съществуващ контейнер: {containerPath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Грешка при зареждане: {ex.Message}");
                    Console.WriteLine("Създаваме нов контейнер...");
                    File.Delete(containerPath); // Изтрийте повредения
                    CreateNewContainer();
                }
            }

            this.blockManager = new BlockManager(containerPath);
        }
        private void CreateNewContainer()
        {
            using (var fs = new FileStream(containerPath, FileMode.Create))
            using (var writer = new BinaryWriter(fs))
            {
                // Записване на хедъра
                writer.Write(Encoding.ASCII.GetBytes(MAGIC_STRING));
                writer.Write(CURRENT_VERSION);
                writer.Write((uint)0); // Брой записи (ще се обновява)
                writer.Write(METADATA_START);
                writer.Write((uint)0); // Резервирано
                writer.Write((uint)0); // Резервирано
                writer.Write((uint)0); // Резервирано

                // Създаване на root директория
                MetadataEntry root = new MetadataEntry();
                root.id = nextId++;
                root.parentId = 0;
                root.startOffset = 0;
                root.size = 0;
                root.nextBlock = 0;
                root.isDirectory = true;
                root.isDeleted = false;
                root.SetName("");
                root.created = DateTime.Now;
                root.modified = DateTime.Now;
                root.checksum = 0;
                root.compressedSize = 0;

                // Записване на root директорията
                fs.Seek(METADATA_START, SeekOrigin.Begin);
                root.Write(writer);

                // Обновяване на хедъра
                fs.Seek(12, SeekOrigin.Begin);
                writer.Write((uint)1); // Брой записи

                currentDirectoryId = root.id;
            }
        }

        private void LoadExistingContainer()
        {
            using (var fs = new FileStream(containerPath, FileMode.Open))
            using (var reader = new BinaryReader(fs))
            {
                // Проверка дали файлът е достатъчно голям
                if (fs.Length < MAGIC_STRING.Length)
                {
                    throw new InvalidDataException("Файлът е твърде малък за контейнер");
                }

                // Проверка на магическия стринг
                byte[] magicBytes = reader.ReadBytes(MAGIC_STRING.Length);
                string magic = Encoding.ASCII.GetString(magicBytes);

                if (magic != MAGIC_STRING)
                {
                    throw new InvalidDataException("Невалиден контейнер файл!");
                }

                reader.ReadUInt32(); // Версия
                uint entryCount = reader.ReadUInt32();
                reader.ReadUInt32(); // METADATA_START

                // Намиране на най-голямото ID
                nextId = 1;
                for (uint i = 0; i < entryCount; i++)
                {
                    long position = METADATA_START + i * MetadataEntry.Size;
                    if (position >= fs.Length)
                        break;

                    fs.Seek(position, SeekOrigin.Begin);
                    MetadataEntry entry = MetadataEntry.Read(reader);

                    if (entry.id >= nextId)
                    {
                        nextId = entry.id + 1;
                    }
                }

                currentDirectoryId = 1; // root директория
            }
        }

        // cpin - Копиране на файл в контейнера
        public void CopyIn(string externalPath, string internalName)
        {
            if (!File.Exists(externalPath))
            {
                throw new FileNotFoundException($"Външният файл не съществува: {externalPath}");
            }

            byte[] fileData;
            try
            {
                fileData = File.ReadAllBytes(externalPath);
            }
            catch (Exception ex)
            {
                throw new IOException($"Грешка при четене на файл {externalPath}: {ex.Message}");
            }

            // Изчисляване на контролна сума
            uint checksum = Resiliency.CalculateChecksum(fileData);

            // Компресиране на данните
            byte[] compressedData;
            if (fileData.Length > 1024)
            {
                compressedData = Compression.CompressData(fileData);
            }
            else
            {
                compressedData = Compression.SimpleCompress(fileData);
            }

            // Намиране на място за данните
            uint dataOffset = blockManager.FindFreeBlock((uint)compressedData.Length);

            using (var fs = new FileStream(containerPath, FileMode.Open))
            using (var writer = new BinaryWriter(fs))
            using (var reader = new BinaryReader(fs))
            {
                // Транзакционен запис: първо данните, после метаданните
                try
                {
                    // Записване на данните
                    fs.Seek(dataOffset, SeekOrigin.Begin);
                    writer.Write(compressedData);

                    // Четене на броя записи
                    fs.Seek(12, SeekOrigin.Begin);
                    uint entryCount = reader.ReadUInt32();

                    // Създаване на метаданни
                    MetadataEntry metadata = new MetadataEntry();
                    metadata.id = nextId++;
                    metadata.parentId = currentDirectoryId;
                    metadata.startOffset = dataOffset;
                    metadata.size = (uint)fileData.Length;
                    metadata.nextBlock = 0;
                    metadata.isDirectory = false;
                    metadata.isDeleted = false;
                    metadata.SetName(internalName);
                    metadata.created = DateTime.Now;
                    metadata.modified = DateTime.Now;
                    metadata.checksum = checksum;
                    metadata.compressedSize = (uint)compressedData.Length;

                    // Записване на метаданните
                    fs.Seek(METADATA_START + entryCount * MetadataEntry.Size, SeekOrigin.Begin);
                    metadata.Write(writer);

                    // Обновяване на броя записи
                    fs.Seek(12, SeekOrigin.Begin);
                    writer.Write(entryCount + 1);
                }
                catch (Exception)
                {
                    // При грешка маркираме блока като свободен
                    blockManager.FreeBlockAt(dataOffset, (uint)compressedData.Length);
                    throw;
                }
            }
        }

        // ls - Извежда съдържанието на контейнера
        public List<string> ListContents()
        {
            List<string> result = new List<string>();

            using (var fs = new FileStream(containerPath, FileMode.Open))
            using (var reader = new BinaryReader(fs))
            {
                fs.Seek(12, SeekOrigin.Begin);
                uint entryCount = reader.ReadUInt32();

                for (uint i = 0; i < entryCount; i++)
                {
                    fs.Seek(METADATA_START + i * MetadataEntry.Size, SeekOrigin.Begin);
                    MetadataEntry entry = MetadataEntry.Read(reader);

                    if (!entry.isDeleted && entry.parentId == currentDirectoryId)
                    {
                        string type = entry.isDirectory ? "<DIR>" : "     ";
                        string size = entry.isDirectory ? "" : $"{entry.size}B";
                        string compressedInfo = (entry.compressedSize > 0 && !entry.isDirectory) ?
                            $" ({entry.compressedSize}B compr.)" : "";

                        result.Add($"{entry.GetName(),-30} {type} {size,10}{compressedInfo}");
                    }
                }
            }

            return result;
        }

        // rm - Изтрива файл от контейнера

        public void Remove(string name)
        {
            // 1. Намерете записа
            (bool found, uint entryIndex, MetadataEntry entry) = FindEntryByName(name);

            if (!found)
                throw new FileNotFoundException($"Файлът не съществува: {name}");

            if (entry.isDirectory)
                throw new InvalidOperationException($"'{name}' е директория. Използвайте 'rd' команда.");

            // 2. Маркирайте като изтрит
            MarkEntryAsDeleted(entryIndex, entry);

            // 3. Освободете блока
            blockManager.FreeBlockAt(entry.startOffset, entry.compressedSize);
        }

        // Нов помощен метод за намиране на запис
        private (bool found, uint index, MetadataEntry entry) FindEntryByName(string name)
        {
            using (var fs = new FileStream(containerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(fs))
            {
                fs.Seek(12, SeekOrigin.Begin);
                uint entryCount = reader.ReadUInt32();

                for (uint i = 0; i < entryCount; i++)
                {
                    long position = METADATA_START + i * MetadataEntry.Size;
                    if (position >= fs.Length) break;

                    fs.Seek(position, SeekOrigin.Begin);
                    MetadataEntry entry = MetadataEntry.Read(reader);

                    if (!entry.isDeleted &&
                        entry.parentId == currentDirectoryId &&
                        !entry.isDirectory &&
                        entry.GetName() == name)
                    {
                        return (true, i, entry);
                    }
                }
            }

            return (false, 0, default);
        }

        // Нов помощен метод за маркиране като изтрит
        private void MarkEntryAsDeleted(uint index, MetadataEntry entry)
        {
            entry.isDeleted = true;

            using (var fs = new FileStream(containerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (var writer = new BinaryWriter(fs))
            {
                long position = METADATA_START + index * MetadataEntry.Size;
                fs.Seek(position, SeekOrigin.Begin);
                entry.Write(writer);
            }
        }

        // cpout - Копира файл от контейнера
        public void CopyOut(string internalName, string externalPath)
        {
            using (var fs = new FileStream(containerPath, FileMode.Open))
            using (var reader = new BinaryReader(fs))
            {
                fs.Seek(12, SeekOrigin.Begin);
                uint entryCount = reader.ReadUInt32();

                MetadataEntry foundEntry = new MetadataEntry();
                bool found = false;

                // Търсене на файла
                for (uint i = 0; i < entryCount; i++)
                {
                    fs.Seek(METADATA_START + i * MetadataEntry.Size, SeekOrigin.Begin);
                    MetadataEntry entry = MetadataEntry.Read(reader);

                    if (!entry.isDeleted && entry.parentId == currentDirectoryId &&
                        !entry.isDirectory && entry.GetName() == internalName)
                    {
                        foundEntry = entry;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    throw new FileNotFoundException($"Файлът не съществува: {internalName}");
                }

                // Четене на компресираните данни
                fs.Seek(foundEntry.startOffset, SeekOrigin.Begin);
                byte[] compressedData = reader.ReadBytes((int)foundEntry.compressedSize);

                // Декомпресиране
                byte[] originalData;
                if (foundEntry.compressedSize < foundEntry.size)
                {
                    originalData = Compression.DecompressData(compressedData, foundEntry.size);
                }
                else
                {
                    originalData = Compression.SimpleDecompress(compressedData);
                }

                // Проверка за целост
                if (!Resiliency.VerifyIntegrity(originalData, foundEntry.checksum))
                {
                    throw new InvalidDataException("Данните са повредени!");
                }

                // Записване на външния файл
                try
                {
                    File.WriteAllBytes(externalPath, originalData);
                }
                catch (Exception ex)
                {
                    throw new IOException($"Грешка при запис на файл {externalPath}: {ex.Message}");
                }
            }
        }

        // md - Създава директория
        public void CreateDirectory(string name)
        {
            using (var fs = new FileStream(containerPath, FileMode.Open))
            using (var writer = new BinaryWriter(fs))
            using (var reader = new BinaryReader(fs))
            {
                // Проверка дали вече съществува
                fs.Seek(12, SeekOrigin.Begin);
                uint entryCount = reader.ReadUInt32();

                for (uint i = 0; i < entryCount; i++)
                {
                    fs.Seek(METADATA_START + i * MetadataEntry.Size, SeekOrigin.Begin);
                    MetadataEntry entry = MetadataEntry.Read(reader);

                    if (!entry.isDeleted && entry.parentId == currentDirectoryId &&
                        entry.isDirectory && entry.GetName() == name)
                    {
                        throw new InvalidOperationException($"Директорията вече съществува: {name}");
                    }
                }

                // Създаване на нова директория
                MetadataEntry metadata = new MetadataEntry();
                metadata.id = nextId++;
                metadata.parentId = currentDirectoryId;
                metadata.startOffset = 0;
                metadata.size = 0;
                metadata.nextBlock = 0;
                metadata.isDirectory = true;
                metadata.isDeleted = false;
                metadata.SetName(name);
                metadata.created = DateTime.Now;
                metadata.modified = DateTime.Now;
                metadata.checksum = 0;
                metadata.compressedSize = 0;

                // Записване на метаданните
                fs.Seek(METADATA_START + entryCount * MetadataEntry.Size, SeekOrigin.Begin);
                metadata.Write(writer);

                // Обновяване на броя записи
                fs.Seek(12, SeekOrigin.Begin);
                writer.Write(entryCount + 1);
            }
        }

        // cd - Промяна на текущата директория
        public void ChangeDirectory(string path)
        {
            if (path == "..")
            {
                // Навигиране нагоре
                using (var fs = new FileStream(containerPath, FileMode.Open))
                using (var reader = new BinaryReader(fs))
                {
                    fs.Seek(12, SeekOrigin.Begin);
                    uint entryCount = reader.ReadUInt32();

                    for (uint i = 0; i < entryCount; i++)
                    {
                        fs.Seek(METADATA_START + i * MetadataEntry.Size, SeekOrigin.Begin);
                        MetadataEntry entry = MetadataEntry.Read(reader);

                        if (entry.id == currentDirectoryId && entry.parentId != 0)
                        {
                            currentDirectoryId = entry.parentId;
                            return;
                        }
                    }
                }
            }
            else if (path == "\\" || path == "/")
            {
                currentDirectoryId = 1; // root
            }
            else
            {
                // Навигиране към поддиректория
                using (var fs = new FileStream(containerPath, FileMode.Open))
                using (var reader = new BinaryReader(fs))
                {
                    fs.Seek(12, SeekOrigin.Begin);
                    uint entryCount = reader.ReadUInt32();

                    for (uint i = 0; i < entryCount; i++)
                    {
                        fs.Seek(METADATA_START + i * MetadataEntry.Size, SeekOrigin.Begin);
                        MetadataEntry entry = MetadataEntry.Read(reader);

                        if (!entry.isDeleted && entry.parentId == currentDirectoryId &&
                            entry.isDirectory && entry.GetName() == path)
                        {
                            currentDirectoryId = entry.id;
                            return;
                        }
                    }

                    throw new DirectoryNotFoundException($"Директорията не съществува: {path}");
                }
            }
        }

        // rd - Изтрива директория
        public void RemoveDirectory(string name)
        {
            // 1. Намерете директорията
            (bool found, uint dirIndex, MetadataEntry dirEntry) = FindDirectoryByName(name);

            if (!found)
                throw new DirectoryNotFoundException($"Директорията не съществува: {name}");

            // 2. Намерете и изтрийте всичко в нея
            List<uint> entriesToDelete = FindAllEntriesInDirectory(dirEntry.id);
            entriesToDelete.Add(dirIndex);

            // 3. Маркирайте всичко като изтрито
            foreach (uint index in entriesToDelete)
            {
                MarkEntryAsDeleted(index, GetEntryAtIndex(index));
            }
        }

        // Помощни методи за RemoveDirectory
        private (bool found, uint index, MetadataEntry entry) FindDirectoryByName(string name)
        {
            using (var fs = new FileStream(containerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(fs))
            {
                fs.Seek(12, SeekOrigin.Begin);
                uint entryCount = reader.ReadUInt32();

                for (uint i = 0; i < entryCount; i++)
                {
                    long position = METADATA_START + i * MetadataEntry.Size;
                    if (position >= fs.Length) break;

                    fs.Seek(position, SeekOrigin.Begin);
                    MetadataEntry entry = MetadataEntry.Read(reader);

                    if (!entry.isDeleted &&
                        entry.parentId == currentDirectoryId &&
                        entry.isDirectory &&
                        entry.GetName() == name)
                    {
                        return (true, i, entry);
                    }
                }
            }

            return (false, 0, default);
        }

        private List<uint> FindAllEntriesInDirectory(uint dirId)
        {
            List<uint> result = new List<uint>();

            using (var fs = new FileStream(containerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(fs))
            {
                fs.Seek(12, SeekOrigin.Begin);
                uint entryCount = reader.ReadUInt32();

                for (uint i = 0; i < entryCount; i++)
                {
                    long position = METADATA_START + i * MetadataEntry.Size;
                    if (position >= fs.Length) break;

                    fs.Seek(position, SeekOrigin.Begin);
                    MetadataEntry entry = MetadataEntry.Read(reader);

                    if (!entry.isDeleted && entry.parentId == dirId)
                    {
                        result.Add(i);
                    }
                }
            }

            return result;
        }

        private MetadataEntry GetEntryAtIndex(uint index)
        {
            using (var fs = new FileStream(containerPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(fs))
            {
                long position = METADATA_START + index * MetadataEntry.Size;
                fs.Seek(position, SeekOrigin.Begin);
                return MetadataEntry.Read(reader);
            }
        }

        // Дефрагментация
        public void Defragment()
        {
            blockManager.Defragment();
        }

        // Информация за контейнера
        public string GetContainerInfo()
        {
            using (var fs = new FileStream(containerPath, FileMode.Open))
            using (var reader = new BinaryReader(fs))
            {
                fs.Seek(12, SeekOrigin.Begin);
                uint entryCount = reader.ReadUInt32();

                long containerSize = fs.Length;

                return $"Размер на контейнера: {containerSize} байта\n" +
                       $"Брой записи: {entryCount}\n" +
                       $"Текуща директория ID: {currentDirectoryId}";
            }
        }
        // Нов метод за създаване на контейнер, ако не може да се зареди
        public static void CreateContainer(string containerPath)
        {
            using (var fs = new FileStream(containerPath, FileMode.Create))
            using (var writer = new BinaryWriter(fs))
            {
                // Записване на хедъра
                writer.Write(Encoding.ASCII.GetBytes("FSCONTAINER"));
                writer.Write((uint)1); // Версия
                writer.Write((uint)0); // Брой записи
                writer.Write((uint)2048); // METADATA_START
                writer.Write((uint)0); // Резервирано
                writer.Write((uint)0); // Резервирано
                writer.Write((uint)0); // Резервирано
            }
        }
    }
}