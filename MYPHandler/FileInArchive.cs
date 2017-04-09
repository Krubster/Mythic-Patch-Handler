namespace MYPHandler
{
    public class FileInArchive
    {
        public FileInArchiveDescriptor descriptor = new FileInArchiveDescriptor();
        public byte[] data_start_200 = new byte[200];
        public string sourceFileName = "";
        private FileInArchiveState state = FileInArchiveState.UNCHANGED;
        public byte[] metadata;
        public byte[] data;

        public long Offset
        {
            get
            {
                return this.descriptor.startingPosition;
            }
        }

        public uint Size
        {
            get
            {
                return this.descriptor.uncompressedSize;
            }
        }

        public uint CompressedSize
        {
            get
            {
                return this.descriptor.compressedSize;
            }
        }

        public byte CompressionMethod
        {
            get
            {
                return this.descriptor.compressionMethod;
            }
        }

        public string Filename
        {
            get
            {
                return this.descriptor.filename;
            }
        }

        public string Extension
        {
            get
            {
                return this.descriptor.extension;
            }
        }

        public FileInArchiveState State
        {
            get
            {
                return this.state;
            }
            set
            {
                this.state = value;
            }
        }
    }
}
