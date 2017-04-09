using System;

namespace MYPHandler
{
    public class BufferObject
    {
        public byte[] buffer;
        public bool trueFileName;
        public string filename;
        public string ext;

        public BufferObject(byte[] buffer, bool trueFileName, string filename, string ext)
        {
            this.buffer = new byte[buffer.Length];
            buffer.CopyTo((Array)this.buffer, 0);
            this.trueFileName = trueFileName;
            this.filename = filename;
            this.ext = ext;
        }
    }
}
