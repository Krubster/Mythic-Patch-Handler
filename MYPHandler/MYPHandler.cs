using ICSharpCode.SharpZipLib.Zip.Compression;
using nsHashDictionary;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Text;
using System.Threading;

namespace MYPHandler
{
    public class MYPHandler
    {
        public List<FileInArchive> archiveFileList = new List<FileInArchive>();
        public List<FileInArchive> archiveNewFileList = new List<FileInArchive>();
        public List<FileInArchive> archiveModifiedFileList = new List<FileInArchive>();
        private string pattern = "*";
        private long unCompressedSize = 0;
        private long numberOfFileNamesFound = 0;
        private long totalNumberOfFiles = 0;
        private long numberOfFilesFound = 0;
        private long error_FileEntryNumber = 0;
        private string extractionPath = "";
        private double totalMemory = 0.0;
        private double programMemory = 1000000000.0;
        private ManagementScope oMs = new ManagementScope();
        private ObjectQuery oQuery = new ObjectQuery("SELECT Capacity FROM Win32_PhysicalMemory");
        private long error_ExtractionNumber = 0;
        private long numExtractedFiles = 0;
        private int numOfFileInExtractionList = 0;
        protected PerformanceCounter ramCounter;
        private int garbageRuns = 0;
        private HashDictionary hashDictionary;
        public FileStream archiveStream;
        private string currentMypFileName;
        public string fullMypFileName;
        private string mypPath;
        private long tableStart;
        private ManagementObjectSearcher oSearcher;
        private ManagementObjectCollection oReturnCollection;
        private float usedRam;
        private float oldUsedRam;
        private BufferObjectList boList;

        public string ExtractionPath
        {
            get
            {
                return this.extractionPath;
            }
            set
            {
                this.extractionPath = value;
            }
        }

        public string Pattern
        {
            get
            {
                return this.pattern;
            }
            set
            {
                this.pattern = value;
            }
        }

        public long UnCompressedSize
        {
            get
            {
                return this.unCompressedSize;
            }
        }

        public long NumberOfFileNamesFound
        {
            get
            {
                return this.numberOfFileNamesFound;
            }
        }

        public long TotalNumberOfFiles
        {
            get
            {
                return this.totalNumberOfFiles;
            }
        }

        public long NumberOfFilesFound
        {
            get
            {
                return this.numberOfFilesFound;
            }
        }

        public long Error_FileEntryNumber
        {
            get
            {
                return this.error_FileEntryNumber;
            }
        }

        public long Error_ExtractionNumber
        {
            get
            {
                return this.error_ExtractionNumber;
            }
        }

        public event del_FileTableEventHandler event_FileTable;

        public event del_FileEventHandler event_Extraction;

        public MYPHandler(string filename, del_FileTableEventHandler eventHandler_FileTable, del_FileEventHandler eventHandler_Extraction, HashDictionary hashDic)
        {
            // Try to set ramCounter to the performance counter but if it fails set it to null to default getUsedRAM() to 0
            try
            {
                ramCounter = new PerformanceCounter("Process", "Private Bytes", Process.GetCurrentProcess().ProcessName);
            }
            catch
            {
                ramCounter = null;
            }
            this.hashDictionary = hashDic;
            if (eventHandler_Extraction != null)
                this.event_Extraction += eventHandler_Extraction;
            if (eventHandler_FileTable != null)
                this.event_FileTable += eventHandler_FileTable;
            this.currentMypFileName = filename.Substring(filename.LastIndexOf('\\') + 1, filename.Length - filename.LastIndexOf('\\') - 1);
            this.currentMypFileName = this.currentMypFileName.Split('.')[0];
            this.fullMypFileName = filename;
            this.mypPath = filename.LastIndexOf('\\') < 0 ? "" : filename.Substring(0, filename.LastIndexOf('\\'));
            this.pattern = "*";
            this.unCompressedSize = 0L;
            this.numberOfFileNamesFound = 0L;
            this.totalNumberOfFiles = 0L;
            this.numberOfFilesFound = 0L;
            this.error_FileEntryNumber = 0L;
            this.error_ExtractionNumber = 0L;
            this.archiveStream = new FileStream(filename, FileMode.Open, FileAccess.Read);
            this.archiveStream.Seek(12L, SeekOrigin.Begin);
            byte[] numArray = new byte[8];
            this.archiveStream.Read(numArray, 0, numArray.Length);
            this.tableStart = (long)FileInArchiveDescriptor.convertLittleEndianBufferToInt(numArray, 0L);
            this.tableStart += (long)FileInArchiveDescriptor.convertLittleEndianBufferToInt(numArray, 4L) << 32;
            this.GetFileNumber();
            this.oSearcher = new ManagementObjectSearcher(this.oMs, this.oQuery);
            this.oReturnCollection = this.oSearcher.Get();
            foreach (ManagementBaseObject oReturn in this.oReturnCollection)
                this.totalMemory += Convert.ToDouble(oReturn["Capacity"]);
            if (this.totalMemory > this.programMemory)
                return;
            this.programMemory = this.totalMemory / 2.0;
        }

        private void TriggerFileTableEvent(MYPFileTableEventArgs e)
        {
            if (this.event_FileTable == null)
                return;
            this.event_FileTable((object)this, e);
        }

        private void Error_FileTableEntry(FileInArchive archFile)
        {
            ++this.error_FileEntryNumber;
            this.TriggerFileTableEvent(new MYPFileTableEventArgs(Event_FileTableType.FileError, archFile));
        }

        public void Dispose()
        {
            if (this.archiveStream == null)
                return;
            this.archiveStream.Close();
        }

        private void GetFileNumber()
        {
            this.archiveStream.Seek(24L, SeekOrigin.Begin);
            byte[] numArray = new byte[4];
            this.archiveStream.Read(numArray, 0, numArray.Length);
            this.totalNumberOfFiles = (long)FileInArchiveDescriptor.convertLittleEndianBufferToInt(numArray, 0L);
        }

        public void ScanFileTable()
        {
            this.GetFileTable();
            this.TriggerExtractionEvent(new MYPFileEventArgs(Event_ExtractionType.Scanning, 0L));
        }

        public void GetFileTable()
        {
            this.error_FileEntryNumber = 0L;
            this.error_ExtractionNumber = 0L;
            this.unCompressedSize = 0L;
            this.numberOfFileNamesFound = 0L;
            this.numberOfFilesFound = 0L;
            byte[] numArray = new byte[12];
            byte[] buffer = new byte[FileInArchiveDescriptor.fileDescriptorSize];
            while (this.tableStart != 0L)
            {
                this.archiveStream.Seek(this.tableStart, SeekOrigin.Begin);
                this.archiveStream.Read(numArray, 0, numArray.Length);
                uint num1 = FileInArchiveDescriptor.convertLittleEndianBufferToInt(numArray, 0L);
                long offset = this.tableStart + 12L;
                long num2 = this.tableStart + 12L + (long)FileInArchiveDescriptor.fileDescriptorSize * (long)num1;
                this.tableStart = (long)FileInArchiveDescriptor.convertLittleEndianBufferToInt(numArray, 4L);
                this.tableStart += (long)FileInArchiveDescriptor.convertLittleEndianBufferToInt(numArray, 8L) << 32;
                while (offset < num2)
                {
                    this.archiveStream.Seek(offset, SeekOrigin.Begin);
                    this.archiveStream.Read(buffer, 0, buffer.Length);
                    FileInArchive archFile = new FileInArchive();
                    archFile.descriptor = new FileInArchiveDescriptor(buffer);
                    archFile.sourceFileName = this.fullMypFileName;
                    archFile.descriptor.fileTableEntryPosition = offset;
                    if (archFile.descriptor.startingPosition > 0L && archFile.descriptor.compressedSize > 0U && archFile.descriptor.uncompressedSize > 0U)
                    {
                        if (this.hashDictionary != null)
                        {
                            HashData hashData = this.hashDictionary.SearchHashList(archFile.descriptor.ph, archFile.descriptor.sh);
                            if (hashData != null && (string)hashData.filename != "")
                            {
                                archFile.descriptor.foundFileName = true;
                                archFile.descriptor.filename = (string)hashData.filename;
                                if (archFile.descriptor.crc != hashData.crc)
                                {
                                    archFile.State = FileInArchiveState.MODIFIED;
                                    this.hashDictionary.UpdateCRC(archFile.descriptor.ph, archFile.descriptor.sh, archFile.descriptor.crc);
                                    this.archiveModifiedFileList.Add(archFile);
                                }
                                ++this.numberOfFileNamesFound;
                                int num3 = archFile.descriptor.filename.LastIndexOf('.');
                                archFile.descriptor.extension = archFile.descriptor.filename.Substring(num3 + 1);
                            }
                            else
                            {
                                if (hashData == null)
                                {
                                    archFile.State = FileInArchiveState.NEW;
                                    this.hashDictionary.AddHash(archFile.descriptor.ph, archFile.descriptor.sh, "", archFile.descriptor.crc);
                                    this.archiveNewFileList.Add(archFile);
                                }
                                else if (archFile.descriptor.crc != hashData.crc)
                                {
                                    archFile.State = FileInArchiveState.MODIFIED;
                                    this.hashDictionary.UpdateCRC(archFile.descriptor.ph, archFile.descriptor.sh, archFile.descriptor.crc);
                                    this.archiveModifiedFileList.Add(archFile);
                                }
                                archFile.metadata = new byte[archFile.descriptor.fileHeaderSize];
                                this.archiveStream.Seek(archFile.descriptor.startingPosition, SeekOrigin.Begin);
                                this.archiveStream.Read(archFile.metadata, 0, archFile.metadata.Length);
                                if (!archFile.descriptor.foundFileName)
                                {
                                    if ((long)archFile.descriptor.compressedSize < (long)archFile.data_start_200.Length)
                                        archFile.data_start_200 = new byte[archFile.descriptor.compressedSize];
                                    this.archiveStream.Seek(archFile.descriptor.startingPosition + (long)archFile.descriptor.fileHeaderSize, SeekOrigin.Begin);
                                    this.archiveStream.Read(archFile.data_start_200, 0, archFile.data_start_200.Length);
                                    try
                                    {
                                        this.TreatHeader(archFile);
                                    }
                                    catch (Exception ex)
                                    {
                                        this.Error_FileTableEntry(archFile);
                                    }
                                }
                            }
                            try
                            {
                                ++this.numberOfFilesFound;
                                if (this.WildcardMatch(this.pattern, archFile.Filename))
                                {
                                    this.archiveFileList.Add(archFile);
                                    this.unCompressedSize += (long)archFile.descriptor.uncompressedSize;
                                    this.TriggerFileTableEvent(new MYPFileTableEventArgs(Event_FileTableType.NewFile, archFile));
                                }
                                else
                                    this.TriggerFileTableEvent(new MYPFileTableEventArgs(Event_FileTableType.UpdateFile, (FileInArchive)null));
                            }
                            catch (Exception ex)
                            {
                                this.Error_FileTableEntry(archFile);
                            }
                        }
                    }
                    offset += (long)FileInArchiveDescriptor.fileDescriptorSize;
                }
            }
            this.TriggerFileTableEvent(new MYPFileTableEventArgs(Event_FileTableType.Finished, (FileInArchive)null));
        }

        private void TreatHeader(FileInArchive archFile)
        {
            MemoryStream memoryStream = new MemoryStream();
            byte[] buffer = new byte[4096];
            if ((int)archFile.descriptor.compressionMethod == 1)
            {
                try
                {
                    Inflater inflater = new Inflater();
                    inflater.SetInput(archFile.data_start_200);
                    inflater.Inflate(buffer);
                }
                catch (Exception ex)
                {
                    this.Error_FileTableEntry(archFile);
                }
                archFile.descriptor.extension = this.GetExtension(buffer);
            }
            else
            {
                if ((int)archFile.descriptor.compressionMethod != 0)
                    return;
                archFile.descriptor.extension = this.GetExtension(archFile.data_start_200);
            }
        }

        private string GetExtension(byte[] buffer)
        {
            byte[] bytes = buffer;
            string str1 = Encoding.ASCII.GetString(bytes, 0, bytes.Length);
            char[] separator = new char[1] { ',' };
            int length = str1.Split(separator, 10).Length;
            string str2 = Encoding.ASCII.GetString(bytes, 0, 4);
            string str3 = "txt";
            if ((int)bytes[0] == 0 && (int)bytes[1] == 1 && (int)bytes[2] == 0)
                str3 = "ttf";
            else if ((int)bytes[0] == 10 && (int)bytes[1] == 5 && (int)bytes[2] == 1 && (int)bytes[3] == 8)
                str3 = "pcx";
            else if (str2.IndexOf("PK") >= 0)
                str3 = "zip";
            else if (str2.IndexOf("<") >= 0)
                str3 = "xml";
            else if (str1.IndexOf("lua") >= 0 && str1.IndexOf("lua") < 50)
                str3 = "lua";
            else if (str2.IndexOf("DDS") >= 0)
                str3 = "dds";
            else if (str2.IndexOf("XSM") >= 0)
                str3 = "xsm";
            else if (str2.IndexOf("XAC") >= 0)
                str3 = "xac";
            else if (str2.IndexOf("8BPS") >= 0)
                str3 = "8bps";
            else if (str2.IndexOf("bdLF") >= 0)
                str3 = "db";
            else if (str2.IndexOf("gsLF") >= 0)
                str3 = "geom";
            else if (str2.IndexOf("idLF") >= 0)
                str3 = "diffuse";
            else if (str2.IndexOf("psLF") >= 0)
                str3 = "specular";
            else if (str2.IndexOf("amLF") >= 0)
                str3 = "mask";
            else if (str2.IndexOf("ntLF") >= 0)
                str3 = "tint";
            else if (str2.IndexOf("lgLF") >= 0)
                str3 = "glow";
            else if (str1.IndexOf("Gamebry") >= 0)
                str3 = "nif";
            else if (str1.IndexOf("WMPHOTO") >= 0)
                str3 = "lmp";
            else if (str2.IndexOf("RIFF") >= 0)
                str3 = Encoding.ASCII.GetString(bytes, 8, 4).IndexOf("WAVE") < 0 ? "riff" : "wav";
            else if (str2.IndexOf("; Zo") >= 0)
                str3 = "zone.txt";
            else if (str2.IndexOf("\0\0\0\0") >= 0)
                str3 = "zero.txt";
            else if (str2.IndexOf("PNG") >= 0)
                str3 = "png";
            else if (length >= 10)
                str3 = "csv";
            return str3;
        }

        public FileInArchive SearchForFile(string filename)
        {
            for (int index = 0; index < this.archiveFileList.Count; ++index)
            {
                if (this.archiveFileList[index].descriptor.filename == filename)
                    return this.archiveFileList[index];
            }
            return (FileInArchive)null;
        }

        public void DumpFileList()
        {
            string path = this.extractionPath + "\\" + Path.GetFileNameWithoutExtension(this.fullMypFileName) + "_FileList.txt";
            if (File.Exists(path))
                File.Delete(path);
            StreamWriter streamWriter = new StreamWriter((Stream)new FileStream(path, FileMode.Create));
            foreach (FileInArchive archiveFile in this.archiveFileList)
            {
                if (archiveFile.descriptor.filename.CompareTo("") != 0)
                    streamWriter.WriteLine(archiveFile.descriptor.filename);
            }
            streamWriter.Close();
        }

        private void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[1];
            int num = 0;
            int count;
            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, count);
                num += count;
            }
            output.Flush();
        }

        public void ReplaceFile(FileInArchive archFile, FileStream newFile)
        {
            byte[] buffer = new byte[newFile.Length];
            newFile.Read(buffer, 0, buffer.Length);
            MemoryStream memoryStream = new MemoryStream(buffer);
            MemoryStream MS = new MemoryStream();
            byte[] numArray = new byte[0];
            int compressionMethod1 = (int)archFile.descriptor.compressionMethod;
            bool flag = true;
            int compressionMethod2 = (int)archFile.descriptor.compressionMethod;
            flag = false;
            archFile.descriptor.compressionMethod = (byte)0;
            this.CopyStream((Stream)memoryStream, (Stream)MS);
            archFile.descriptor.uncompressedSize = (uint)memoryStream.Length;
            this.WriteFileToArchive(archFile, MS);
        }

        private void WriteFileToArchive(FileInArchive archFile, MemoryStream MS)
        {
            this.archiveStream.Close();
            try
            {
                this.archiveStream = new FileStream(this.fullMypFileName, FileMode.Open, FileAccess.ReadWrite);
            }
            catch (Exception ex)
            {
                throw new Exception("You need to stop application currently using the following file: " + this.fullMypFileName);
            }
            this.archiveStream.Seek(archFile.descriptor.fileTableEntryPosition + 12L, SeekOrigin.Begin);
            this.archiveStream.Write(this.ConvertLongToByteArray((int)(MS.Length & (long)uint.MaxValue)), 0, 4);
            this.archiveStream.Seek(archFile.descriptor.fileTableEntryPosition + 16L, SeekOrigin.Begin);
            this.archiveStream.Write(this.ConvertLongToByteArray((int)archFile.descriptor.uncompressedSize & -1), 0, 4);
            this.archiveStream.Seek(archFile.descriptor.fileTableEntryPosition + 32L, SeekOrigin.Begin);
            this.archiveStream.Write(new byte[1]
            {
        archFile.CompressionMethod
            }, 0, 1);
            byte[] buffer1 = MS.GetBuffer();
            if (MS.Length <= (long)archFile.descriptor.compressedSize)
            {
                this.archiveStream.Seek(archFile.descriptor.startingPosition + (long)archFile.descriptor.fileHeaderSize, SeekOrigin.Begin);
                this.archiveStream.Write(buffer1, 0, (int)MS.Length);
            }
            else
            {
                long length = this.archiveStream.Length;
                this.archiveStream.Seek(0L, SeekOrigin.End);
                if (archFile.metadata != null)
                {
                    this.archiveStream.Write(archFile.metadata, 0, archFile.metadata.Length);
                }
                else
                {
                    byte[] buffer2 = new byte[archFile.descriptor.fileHeaderSize];
                    this.archiveStream.Write(buffer2, 0, buffer2.Length);
                }
                this.archiveStream.Seek(0L, SeekOrigin.End);
                this.archiveStream.Write(buffer1, 0, (int)MS.Length);
                this.archiveStream.Seek(archFile.descriptor.fileTableEntryPosition, SeekOrigin.Begin);
                this.archiveStream.Write(this.ConvertLongToByteArray((int)(length & (long)uint.MaxValue)), 0, 4);
                this.archiveStream.Seek(archFile.descriptor.fileTableEntryPosition + 4L, SeekOrigin.Begin);
                this.archiveStream.Write(this.ConvertLongToByteArray((int)(length >> 32 & (long)uint.MaxValue)), 0, 4);
                archFile.descriptor.startingPosition = (long)(int)(length & (long)uint.MaxValue);
            }
            archFile.descriptor.compressedSize = (uint)MS.Length;
            this.archiveStream.Close();
            this.archiveStream = new FileStream(this.fullMypFileName, FileMode.Open, FileAccess.Read);
        }

        private byte[] ConvertLongToByteArray(int a32bitInt)
        {
            return BitConverter.GetBytes(a32bitInt);
        }

        private bool WildcardMatch(string pattern, string path)
        {
            if (pattern == "")
                return true;
            int num1 = 0;
            int num2 = 0;
            char[] charArray1 = pattern.ToCharArray();
            char[] charArray2 = path.ToCharArray();
            bool flag = false;
            label_3:
            int index1 = num1;
            int index2 = num2;
            while (index1 < charArray2.Length)
            {
                if ((int)charArray1[index2] == 42)
                {
                    flag = true;
                    num1 = index1;
                    if ((num2 = index2 + 1) >= charArray1.Length)
                        return true;
                    goto label_3;
                }
                else if ((int)charArray2[index1] == (int)charArray1[index2])
                {
                    ++index1;
                    ++index2;
                }
                else
                {
                    if (!flag)
                        return false;
                    ++num1;
                    goto label_3;
                }
            }
            if ((int)charArray1[index2] == 42)
                ++index2;
            return index2 >= charArray1.Length;
        }

        private void TriggerExtractionEvent(MYPFileEventArgs e)
        {
            if (this.event_Extraction == null)
                return;
            this.event_Extraction((object)this, e);
        }

        public void ExtractAll()
        {
            this.Extract((object)this.archiveFileList);
        }

        public void Extract(object obj)
        {
            this.boList = new BufferObjectList();
            this.numExtractedFiles = 0L;
            List<FileInArchive> fileInArchiveList = new List<FileInArchive>();
            if (obj.GetType() == typeof(List<FileInArchive>))
                fileInArchiveList = (List<FileInArchive>)obj;
            else if (obj.GetType() == typeof(FileInArchive))
                fileInArchiveList.Add((FileInArchive)obj);
            else if (obj.GetType() == typeof(string))
            {
                FileInArchive fileInArchive = this.SearchForFile((string)obj);
                if (fileInArchive == null)
                    return;
                fileInArchiveList.Add(fileInArchive);
            }
            this.numOfFileInExtractionList = fileInArchiveList.Count;
            if ((int)this.mypPath[0] == (int)this.extractionPath[0])
                MypHandlerConfig.MultithreadedExtraction = false;
            if (MypHandlerConfig.MultithreadedExtraction)
            {
                this.boList.Active = true;
                new Thread(new ThreadStart(this.ThreadWrite)).Start();
            }
            for (int index = 0; index < fileInArchiveList.Count; ++index)
                this.ExtractFile(fileInArchiveList[index]);
            if (!MypHandlerConfig.MultithreadedExtraction)
                this.TriggerExtractionEvent(new MYPFileEventArgs(Event_ExtractionType.ExtractionFinished, (long)fileInArchiveList.Count - this.error_ExtractionNumber));
            else
                this.boList.Active = false;
        }

        private float getUsedRAM()
        {
            if (this.ramCounter == null)
                return 0;
            else
                return this.ramCounter.NextValue();
        }

        private void ExtractFile(FileInArchive archFile)
        {
            this.error_ExtractionNumber = 0L;
            if (MypHandlerConfig.MultithreadedExtraction)
            {
                this.usedRam = this.getUsedRAM();
                if ((double)this.usedRam > this.programMemory)
                {
                    this.oldUsedRam = this.usedRam;
                    ++this.garbageRuns;
                    while (this.boList.Count > 100)
                        Thread.Sleep(1000);
                    GC.Collect();
                    this.usedRam = this.getUsedRAM();
                }
            }
            archFile.data = new byte[archFile.descriptor.compressedSize];
            this.archiveStream.Seek(archFile.descriptor.startingPosition + (long)archFile.descriptor.fileHeaderSize, SeekOrigin.Begin);
            for (int index = 0; index < archFile.data.Length; ++index)
                archFile.data[index] = (byte)this.archiveStream.ReadByte();
            this.TreatExtractedFile(archFile);
            archFile.data = (byte[])null;
        }

        private void TreatExtractedFile(FileInArchive archFile)
        {
            try
            {
                if ((int)archFile.descriptor.compressionMethod == 1)
                {
                    try
                    {
                        byte[] buffer = new byte[archFile.descriptor.uncompressedSize];
                        Inflater inflater = new Inflater();
                        inflater.SetInput(archFile.data);
                        inflater.Inflate(buffer);
                        if (!MypHandlerConfig.MultithreadedExtraction)
                            this.SaveBufferToFile(buffer, archFile.descriptor.foundFileName, archFile.descriptor.filename, archFile.descriptor.extension);
                        else
                            this.boList.AddBufferItemToQueue(buffer, archFile.descriptor.foundFileName, archFile.descriptor.filename, archFile.descriptor.extension);
                    }
                    catch (Exception ex)
                    {
                        this.TriggerExtractionEvent(new MYPFileEventArgs(Event_ExtractionType.FileExtractionError, this.error_ExtractionNumber++));
                    }
                }
                else if ((int)archFile.descriptor.compressionMethod == 0)
                {
                    if (!MypHandlerConfig.MultithreadedExtraction)
                        this.SaveBufferToFile(archFile.data, archFile.descriptor.foundFileName, archFile.descriptor.filename, archFile.descriptor.extension);
                    else
                        this.boList.AddBufferItemToQueue(archFile.data, archFile.descriptor.foundFileName, archFile.descriptor.filename, archFile.descriptor.extension);
                }
                else
                    this.TriggerExtractionEvent(new MYPFileEventArgs(Event_ExtractionType.UnknownCompressionMethod, this.error_ExtractionNumber++));
            }
            catch (Exception ex)
            {
                this.TriggerExtractionEvent(new MYPFileEventArgs(Event_ExtractionType.UnknownError, this.error_ExtractionNumber++));
            }
        }

        private void SaveBufferToFile(byte[] buffer, bool trueFileName, string filename, string ext)
        {
            string str1 = "";
            if (buffer.Length > 100 && !trueFileName)
            {
                for (long index = (long)(buffer.Length - 18); index < (long)buffer.Length; ++index)
                    str1 += Convert.ToChar(buffer[index]).ToString();
                if (ext == "txt" && str1.IndexOf("TRUEVISION-XFILE") >= 0)
                    ext = "tga";
            }
            string str2 = this.mypPath;
            if (this.extractionPath != "")
                str2 = this.extractionPath;
            string path1;
            if (!trueFileName)
            {
                if (!Directory.Exists(str2 + "\\" + this.currentMypFileName))
                    Directory.CreateDirectory(str2 + "\\" + this.currentMypFileName);
                if (!Directory.Exists(str2 + "\\" + this.currentMypFileName + "\\" + ext))
                    Directory.CreateDirectory(str2 + "\\" + this.currentMypFileName + "\\" + ext);
                path1 = str2 + "\\" + this.currentMypFileName + "\\" + ext + "\\" + filename + "." + ext;
            }
            else
            {
                filename = filename.Replace('\\', '/');
                string[] strArray = filename.Split('/');
                if (strArray.Length > 1)
                {
                    string path2 = str2 + (object)'/' + strArray[0];
                    if (!Directory.Exists(path2))
                        Directory.CreateDirectory(path2);
                    for (int index = 1; index < strArray.Length - 1; ++index)
                    {
                        path2 = path2 + (object)'/' + strArray[index];
                        if (!Directory.Exists(path2))
                            Directory.CreateDirectory(path2);
                    }
                }
                path1 = str2 + (object)'/' + filename;
            }
            if (File.Exists(path1))
                File.Delete(path1);
            FileStream fileStream = new FileStream(path1, FileMode.Create);
            fileStream.Write(buffer, 0, buffer.Length);
            fileStream.Close();
            buffer = (byte[])null;
            this.TriggerExtractionEvent(new MYPFileEventArgs(Event_ExtractionType.FileExtracted, this.numExtractedFiles++));
        }

        private void ThreadWrite()
        {
            while (this.boList != null && this.boList.Active)
            {
                List<BufferObject> bufferObjectList = this.boList.RemoveBufferItemListFromQueue();
                for (int index = 0; index < bufferObjectList.Count; ++index)
                    this.SaveBufferToFile(bufferObjectList[index].buffer, bufferObjectList[index].trueFileName, bufferObjectList[index].filename, bufferObjectList[index].ext);
                bufferObjectList.Clear();
                if (this.boList != null && this.boList.Collect)
                    this.boList.RunCollect();
            }
            this.TriggerExtractionEvent(new MYPFileEventArgs(Event_ExtractionType.ExtractionFinished, (long)this.numOfFileInExtractionList - this.error_ExtractionNumber));
            if (this.boList != null)
                this.boList.Clear();
            GC.Collect();
        }
    }
}
