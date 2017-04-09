using System;

namespace MYPHandler
{
    public class MYPFileEventArgs : EventArgs
    {
        private Event_ExtractionType state;
        private long value;

        public long Value
        {
            get
            {
                return this.value;
            }
        }

        public Event_ExtractionType State
        {
            get
            {
                return this.state;
            }
        }

        public MYPFileEventArgs(Event_ExtractionType state, long value)
        {
            this.state = state;
            this.value = value;
        }
    }
}
