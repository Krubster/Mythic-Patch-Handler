namespace MYPHandler
{
    public struct MypThreadParam
    {
        public string[] fileNames;
        public int currentFile;

        public MypThreadParam(string[] fileNames, int currenFile)
        {
            this.fileNames = fileNames;
            this.currentFile = currenFile;
        }
    }
}
