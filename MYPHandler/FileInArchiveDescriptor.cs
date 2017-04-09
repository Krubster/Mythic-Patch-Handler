using System;

namespace MYPHandler
{
    public class FileInArchiveDescriptor
    {
        public static int fileDescriptorSize = 34;
        public int crc = 0;
        public byte[] file_hash = new byte[8];
        public string filename = "";
        public bool foundFileName = false;
        public string extension = "";
        public long fileTableEntryPosition;
        public long startingPosition;
        public uint fileHeaderSize;
        public uint compressedSize;
        public uint uncompressedSize;
        public byte compressionMethod;
        public uint ph;
        public uint sh;
        public string strUTF8;
        public string strUTF16;
        public bool isCompressed;

        public FileInArchiveDescriptor()
        {
        }

        public FileInArchiveDescriptor(byte[] buffer)
        {
            this.startingPosition = (long)FileInArchiveDescriptor.convertLittleEndianBufferToInt(buffer, 0L);
            this.startingPosition += (long)FileInArchiveDescriptor.convertLittleEndianBufferToInt(buffer, 4L) << 32;
            this.fileHeaderSize = FileInArchiveDescriptor.convertLittleEndianBufferToInt(buffer, 8L);
            this.compressedSize = FileInArchiveDescriptor.convertLittleEndianBufferToInt(buffer, 12L);
            this.uncompressedSize = FileInArchiveDescriptor.convertLittleEndianBufferToInt(buffer, 16L);
            Array.Copy((Array)buffer, 20, (Array)this.file_hash, 0, 8);
            this.sh = FileInArchiveDescriptor.convertLittleEndianBufferToInt(buffer, 20L);
            this.ph = FileInArchiveDescriptor.convertLittleEndianBufferToInt(buffer, 24L);
            this.crc = BitConverter.ToInt32(buffer, 28);
            this.filename += string.Format("{0:X8}", (object)this.crc);
            this.filename += "_";
            this.filename += string.Format("{0:X16}", (object)BitConverter.ToInt64(this.file_hash, 0));
            this.compressionMethod = buffer[32];
            this.isCompressed = (int)this.compressionMethod != 0;
        }

        public static uint convertLittleEndianBufferToInt(byte[] intBuffer, long offset)
        {
            uint num = 0;
            for (int index = 3; index >= 0; --index)
                num = (num << 8) + (uint)intBuffer[offset + (long)index];
            return num;
        }
    }
}
