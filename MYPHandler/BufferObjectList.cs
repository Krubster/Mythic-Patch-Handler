using System;
using System.Collections.Generic;
using System.Threading;

namespace MYPHandler
{
    public class BufferObjectList
    {
        public static int maxTmpListSize = 100;
        private List<BufferObject> bufferObjectList = new List<BufferObject>();
        private object lock_bufferobject = new object();
        private long buffersize = 0;
        private int smallbuffersize = BufferObjectList.maxTmpListSize;
        private int bigbuffersize = 2500;
        private bool peak = false;
        private bool collect = false;
        private bool active = false;

        public bool Active
        {
            get
            {
                return this.active || this.bufferObjectList.Count > 0;
            }
            set
            {
                this.active = value;
            }
        }

        public int Count
        {
            get
            {
                return this.bufferObjectList.Count;
            }
        }

        public bool Peak
        {
            get
            {
                return this.peak;
            }
        }

        public int BigBufferSize
        {
            get
            {
                return this.bigbuffersize;
            }
        }

        public int SmallBufferSize
        {
            get
            {
                return this.smallbuffersize;
            }
        }

        public bool Collect
        {
            get
            {
                return this.collect;
            }
        }

        public void AddBufferItemToQueue(byte[] buffer, bool trueFileName, string filename, string ext)
        {
            while (this.buffersize > 500000000L && this.bufferObjectList.Count > 2 || this.peak)
                Thread.Sleep(1000);
            lock (this.lock_bufferobject)
            {
                this.bufferObjectList.Add(new BufferObject(buffer, trueFileName, filename, ext));
                this.buffersize += (long)buffer.Length;
            }
            if (this.bufferObjectList.Count <= this.bigbuffersize)
                return;
            this.peak = true;
        }

        public List<BufferObject> RemoveBufferItemListFromQueue()
        {
            List<BufferObject> bufferObjectList = new List<BufferObject>();
            if (this.bufferObjectList.Count <= BufferObjectList.maxTmpListSize && this.active)
                Thread.Sleep(100);
            lock (this.lock_bufferobject)
            {
                for (int index = 0; index < BufferObjectList.maxTmpListSize && index < this.bufferObjectList.Count; ++index)
                {
                    bufferObjectList.Add(this.bufferObjectList[index]);
                    this.buffersize -= (long)this.bufferObjectList[index].buffer.Length;
                }
                this.bufferObjectList.RemoveRange(0, bufferObjectList.Count);
                if (this.peak && this.bufferObjectList.Count < this.smallbuffersize)
                {
                    this.peak = false;
                    this.collect = true;
                }
            }
            return bufferObjectList;
        }

        public BufferObject RemoveBufferItemFromQueue()
        {
            BufferObject bufferObject = (BufferObject)null;
            if (this.bufferObjectList.Count < 1 && this.active)
                Thread.Sleep(10);
            lock (this.lock_bufferobject)
            {
                if (this.bufferObjectList.Count > 0)
                {
                    bufferObject = this.bufferObjectList[0];
                    this.buffersize -= (long)bufferObject.buffer.Length;
                    this.bufferObjectList.RemoveAt(0);
                }
                if (this.peak && this.bufferObjectList.Count < this.smallbuffersize)
                {
                    this.collect = true;
                    this.peak = false;
                }
            }
            return bufferObject;
        }

        public void RunCollect()
        {
            lock (this.lock_bufferobject)
            {
                this.collect = false;
                GC.Collect();
            }
        }

        public void Clear()
        {
            this.bufferObjectList.Clear();
        }
    }
}
