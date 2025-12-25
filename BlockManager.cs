using System;
using System.Collections.Generic;
using System.IO;

namespace FileSystemContainer
{
    // Структура за свободен блок
    public struct FreeBlock
    {
        public uint offset;
        public uint size;
        public uint nextFreeBlock;
    }

    public class BlockManager
    {
        private const uint FREE_BLOCK_LIST_OFFSET = 100; // Позиция на списъка със свободни блокове
        private const uint MAX_BLOCK_SIZE = 65536; // Максимален размер на блок (64KB)

        private string containerPath;

        public BlockManager(string containerPath)
        {
            this.containerPath = containerPath;

            if (!File.Exists(containerPath))
            {
                InitializeFreeBlocks();
            }
        }

        private void InitializeFreeBlocks()
        {
            using (var fs = new FileStream(containerPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (var writer = new BinaryWriter(fs))
            {
                // Записваме празен списък със свободни блокове
                writer.BaseStream.Seek(FREE_BLOCK_LIST_OFFSET, SeekOrigin.Begin);
                writer.Write((uint)0); // Няма свободни блокове
                writer.Write((uint)0); // Позиция на първия свободен блок
            }
        }

        // Намира свободен блок с подходящ размер
        public uint FindFreeBlock(uint sizeNeeded)
        {
            using (var fs = new FileStream(containerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (var reader = new BinaryReader(fs))
            using (var writer = new BinaryWriter(fs))
            {
                fs.Seek(FREE_BLOCK_LIST_OFFSET, SeekOrigin.Begin);
                uint freeBlockCount = reader.ReadUInt32();
                uint currentFreeBlockOffset = reader.ReadUInt32();

                uint prevBlockOffset = 0;
                uint currentOffset = currentFreeBlockOffset;

                // Търсим блок с подходящ размер
                while (currentOffset != 0)
                {
                    fs.Seek(currentOffset, SeekOrigin.Begin);
                    FreeBlock block = ReadFreeBlock(reader);

                    if (block.size >= sizeNeeded)
                    {
                        // Намерен подходящ блок
                        if (block.size > sizeNeeded + 16) // Ако можем да разделим блока
                        {
                            // Създаваме нов свободен блок с остатъка
                            FreeBlock newFreeBlock = new FreeBlock
                            {
                                offset = block.offset + sizeNeeded,
                                size = block.size - sizeNeeded,
                                nextFreeBlock = block.nextFreeBlock
                            };

                            // Записваме новия свободен блок
                            WriteFreeBlock(writer, newFreeBlock, currentOffset);

                            // Обновяваме списъка
                            if (prevBlockOffset == 0)
                            {
                                fs.Seek(FREE_BLOCK_LIST_OFFSET + 4, SeekOrigin.Begin);
                                writer.Write(currentOffset);
                            }
                            else
                            {
                                fs.Seek(prevBlockOffset + 8, SeekOrigin.Begin); // nextFreeBlock полето
                                writer.Write(currentOffset);
                            }

                            // Връщаме само частта, която ни трябва
                            return block.offset;
                        }
                        else
                        {
                            // Използваме целия блок
                            if (prevBlockOffset == 0)
                            {
                                // Премахваме от началото на списъка
                                fs.Seek(FREE_BLOCK_LIST_OFFSET + 4, SeekOrigin.Begin);
                                writer.Write(block.nextFreeBlock);
                            }
                            else
                            {
                                // Премахваме от средата на списъка
                                fs.Seek(prevBlockOffset + 8, SeekOrigin.Begin);
                                writer.Write(block.nextFreeBlock);
                            }

                            // Намаляваме броя на свободните блокове
                            fs.Seek(FREE_BLOCK_LIST_OFFSET, SeekOrigin.Begin);
                            writer.Write(freeBlockCount - 1);

                            return block.offset;
                        }
                    }

                    prevBlockOffset = currentOffset;
                    currentOffset = block.nextFreeBlock;
                }

                // Не е намерен свободен блок - използваме края на файла
                fs.Seek(0, SeekOrigin.End);
                uint endPosition = (uint)fs.Position;

                // Подравняване на 4KB граница
                uint alignedPosition = (endPosition + 4095) & ~(uint)4095;
                return alignedPosition;
            }
        }

        // Освобождава блок
        public void FreeBlockAt(uint offset, uint size)
        {
            using (var fs = new FileStream(containerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (var writer = new BinaryWriter(fs))
            using (var reader = new BinaryReader(fs))
            {
                fs.Seek(FREE_BLOCK_LIST_OFFSET, SeekOrigin.Begin);
                uint freeBlockCount = reader.ReadUInt32();
                uint firstFreeBlock = reader.ReadUInt32();

                // Създаваме нов свободен блок
                FreeBlock newBlock = new FreeBlock
                {
                    offset = offset,
                    size = size,
                    nextFreeBlock = firstFreeBlock
                };

                // Записваме новата глава на списъка
                uint newBlockPosition = FindSpaceForFreeBlock();
                WriteFreeBlock(writer, newBlock, newBlockPosition);

                // Обновяваме хедъра
                fs.Seek(FREE_BLOCK_LIST_OFFSET, SeekOrigin.Begin);
                writer.Write(freeBlockCount + 1);
                writer.Write(newBlockPosition);
            }
        }

        // Дефрагментация
        public void Defragment()
        {
            using (var fs = new FileStream(containerPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            using (var writer = new BinaryWriter(fs))
            using (var reader = new BinaryReader(fs))
            {
                // Прочитаме всички свободни блокове
                fs.Seek(FREE_BLOCK_LIST_OFFSET, SeekOrigin.Begin);
                uint freeBlockCount = reader.ReadUInt32();
                uint currentFreeBlockOffset = reader.ReadUInt32();

                List<FreeBlock> freeBlocks = new List<FreeBlock>();
                uint currentOffset = currentFreeBlockOffset;

                while (currentOffset != 0)
                {
                    fs.Seek(currentOffset, SeekOrigin.Begin);
                    FreeBlock block = ReadFreeBlock(reader);
                    freeBlocks.Add(block);
                    currentOffset = block.nextFreeBlock;
                }

                // Сортираме блоковете по отместване
                freeBlocks.Sort((a, b) => a.offset.CompareTo(b.offset));

                // Обединяваме съседни блокове
                for (int i = 0; i < freeBlocks.Count - 1; i++)
                {
                    FreeBlock current = freeBlocks[i];
                    FreeBlock next = freeBlocks[i + 1];

                    if (current.offset + current.size == next.offset)
                    {
                        // Обединяваме блоковете
                        current.size += next.size;
                        freeBlocks.RemoveAt(i + 1);
                        i--; // Проверяваме отново същия блок
                    }
                }

                // Записваме обновения списък
                if (freeBlocks.Count > 0)
                {
                    uint firstBlockPos = FindSpaceForFreeBlock();
                    uint currentPos = firstBlockPos;

                    for (int i = 0; i < freeBlocks.Count; i++)
                    {
                        FreeBlock block = freeBlocks[i];
                        block.nextFreeBlock = (i < freeBlocks.Count - 1) ? currentPos + 12 : 0;
                        WriteFreeBlock(writer, block, currentPos);
                        currentPos += 12;
                    }

                    fs.Seek(FREE_BLOCK_LIST_OFFSET, SeekOrigin.Begin);
                    writer.Write((uint)freeBlocks.Count);
                    writer.Write(firstBlockPos);
                }
                else
                {
                    fs.Seek(FREE_BLOCK_LIST_OFFSET, SeekOrigin.Begin);
                    writer.Write((uint)0);
                    writer.Write((uint)0);
                }
            }
        }

        private FreeBlock ReadFreeBlock(BinaryReader reader)
        {
            return new FreeBlock
            {
                offset = reader.ReadUInt32(),
                size = reader.ReadUInt32(),
                nextFreeBlock = reader.ReadUInt32()
            };
        }

        private void WriteFreeBlock(BinaryWriter writer, FreeBlock block, uint position)
        {
            writer.BaseStream.Seek(position, SeekOrigin.Begin);
            writer.Write(block.offset);
            writer.Write(block.size);
            writer.Write(block.nextFreeBlock);
        }

        private uint FindSpaceForFreeBlock()
        {
            using (var fs = new FileStream(containerPath, FileMode.Open,FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                fs.Seek(0, SeekOrigin.End);
                uint endPosition = (uint)fs.Position;

                // Подравняване на 12 байта (размер на FreeBlock запис)
                uint alignedPosition = (endPosition + 11) & ~(uint)11;
                return alignedPosition;
            }
        }
    }
}