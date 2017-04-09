using System;

namespace MYPHandler
{
    public class MYPFileTableEventArgs : EventArgs
    {
        private Event_FileTableType type;
        private FileInArchive archFile;

        public FileInArchive ArchFile
        {
            get
            {
                return this.archFile;
            }
        }

        public Event_FileTableType Type
        {
            get
            {
                return this.type;
            }
        }

        public MYPFileTableEventArgs(Event_FileTableType type, FileInArchive archFile)
        {
            this.type = type;
            this.archFile = archFile;
        }
    }
}
