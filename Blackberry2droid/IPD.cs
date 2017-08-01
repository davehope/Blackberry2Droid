using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml;
using Ionic.Zip;


namespace Blackberry2droid
{

    /// <summary>
    /// Extracts an IPD file from a Blackberry Backup (.BBB) file .
    /// </summary>
    class ExtractBBBFile : IDisposable
    {
        public string DatabasePath
        {
            get
            {
                return szDatabasePath;
            }
        }
        private string szDatabasePath;
        public int DatabaseId
        {
            get
            {
                return dDatabaseId;
            }
        }
        private int dDatabaseId;
        private string szTempDir;


        /// <summary>
        /// Extracts 'Manifest.xml' from the file, parses it and provides the path
        /// to the requested database.
        /// </summary>
        /// <param name="szFilePath"></param>
        /// <param name="szRequiredDatabase"></param>
        public ExtractBBBFile(string szFilePath, string szRequiredDatabase)
        {
            string szZipPath = string.Empty;
            szTempDir = Path.Combine( Path.GetTempPath(), Path.GetRandomFileName() );
            
            using (ZipFile objZipFile = ZipFile.Read(szFilePath))
            {
                // Extract Manifest file.
                ZipEntry objZipManifest = objZipFile["Manifest.xml"];
                objZipManifest.Extract(szTempDir, ExtractExistingFileAction.OverwriteSilently);
                
                // Get the path inside the ZIP FILE to the required database.
                szZipPath = getDatabaseUriFromManifest(szRequiredDatabase, Path.Combine(szTempDir, "Manifest.xml"));

                // Extract databse file.
                ZipEntry objZipDatabase = objZipFile[szZipPath];
                objZipDatabase.Extract(szTempDir, ExtractExistingFileAction.OverwriteSilently);
                szDatabasePath = Path.Combine(szTempDir, szZipPath);
                return;
            }

        }
        

        /// <summary>
        /// Parses Manifest.xml and returns the path to the IPD file for the specified database name.
        /// </summary>
        /// <param name="szRequiredDatabase">Database name, e.g. "SMS Messages"</param>
        /// <param name="szManifestPath">Path to extracted Manifest.xml</param>
        /// <returns></returns>
        private string getDatabaseUriFromManifest(string szRequiredDatabase, string szManifestPath )
        {
            XmlDocument xmlDoc = new XmlDocument();
            string szZipPath = string.Empty;

            xmlDoc.Load(szManifestPath );
            XmlNodeList xmlNodeL = xmlDoc.SelectNodes("/BlackBerry_Backup/NessusOSDevice/Databases/Database[. = '" + szRequiredDatabase + "']");
            foreach (XmlNode xmlNode in xmlNodeL)
            {
                int dThisDatabaseId = Convert.ToInt32( xmlNode.Attributes["uid"].Value );
                dDatabaseId = dThisDatabaseId;
                szZipPath = "Databases\\" + dThisDatabaseId + ".dat";
                break;                  
            }
            return szZipPath;
        }


        /// <summary>
        /// Deletes any resources created by this class.
        /// </summary>
        public void Dispose()
        {
            // Delete extracted database and other extracted info for security / privacy reasons.
            try
            {
                Directory.Delete(szTempDir, true);
            }
            catch {}
        }
    }

    /// <summary>
    /// Parses Research In Motion IPD file format.
    /// </summary>
    class ReadIPD : IDisposable
    {
        public enum RecordTypes { SMS };
        public struct IPD_SMSRecord
        {
            private DateTime _Sent;
            public long SentEpoch;
            public DateTime Sent
            {
                get
                {
                    return _Sent;
                }
            }
            private DateTime _Received;
            public long ReceivedEpoch;
            public DateTime Received
            {
                get
                {
                    return _Received;
                }
            }
            private bool _WasSent;
            public bool WasSent
            {
                get
                {
                    return _WasSent;
                }
            }
            private string _Number;
            public string Number
            {
                get
                {
                    return _Number;
                }
            }
            public string Message
            {
                get
                {
                    if (this.rawMessage == null)
                        return String.Empty;

                    if (this.MessageEncoding.Equals("UCS-2"))
                    {
                        if (this.rawMessage == null)
                            return String.Empty;
                        return Encoding.BigEndianUnicode.GetString(this.rawMessage);
                    }
                    else // "GSM 03.38"
                    {
                        Encoding gsmEnc = new Mediaburst.Text.GSMEncoding();
                        Encoding utf8Enc = new System.Text.UTF8Encoding();

                        byte[] gsmBytes = this.rawMessage;
                        byte[] utf8Bytes = Encoding.Convert(gsmEnc, utf8Enc, gsmBytes);
                        return utf8Enc.GetString(utf8Bytes);
                    }
                }
            }
            private string _MessageEncoding;
            public string MessageEncoding
            {
                get
                {
                    return _MessageEncoding;
                }
            }
            private byte[] _rawMessage;
            public byte[] rawMessage
            {
                get
                {
                    return _rawMessage;
                }
            }

            public void addField(int fieldType, int fieldLength, byte[] fieldData)
            {
                /*
                 *  1:  Date Info
                 *  2:  Telephone Number.
                 *  3:  
                 *  4:  Message (String)
                 *  7:  UCS-2 Field
                 *  9:  Some kind of sequence number.
                 * 11:  Unknown.
                 * 
                 */
                switch (fieldType)
                {
                    case 1:
                        // This field is all about the dates :(
                        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                        long lDateA = 0;
                        for (int i = 13; i < 21; i++)
                        {
                            lDateA |= (long)fieldData[i] << ((i - 13) * 8);
                        }
                        long lDateB = 0;
                        for (int i = 21; i < 29; i++)
                        {
                            lDateB |= (long)fieldData[i] << ((i - 21) * 8);
                        }
                        if (fieldData[0] == 0)
                        {
                            this._WasSent = true;
                            this._Sent = epoch.AddMilliseconds(lDateA);
                            this.SentEpoch = lDateA;
                            this._Received = epoch.AddMilliseconds(lDateB);
                            this.ReceivedEpoch = lDateB;
                        }
                        else
                        {
                            this._WasSent = false;
                            this._Sent = epoch.AddMilliseconds(lDateB);
                            this.SentEpoch = lDateB;
                            this._Received = epoch.AddMilliseconds(lDateA);
                            this.ReceivedEpoch = lDateB;
                        }
                        break;
                    case 2:
                        this._Number = System.Text.ASCIIEncoding.ASCII.GetString(fieldData, 4, fieldData.Length - 5);
                        break;
                    case 4:
                        this._rawMessage = fieldData;
                        break;
                    case 7:
                        if (fieldData[0] == 2)
                        {
                            this._MessageEncoding = "UCS-2";
                        }
                        else
                        {
                            this._MessageEncoding = "GSM 03.38";
                        }
                        break;
                }
            }
        }
        private struct IPD_Record
        {
            public uint DatabaseVersion;
            public ushort DatabaseRecordhandle;
            public ulong RecordUiniqueId;
            public byte[] RecordData;
        }


        /// <summary>
        /// Compares two character arranys and reuturns whether they're identical.
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        bool isDupliateCharArray(char[] first, char[] second)
        {
            if (first.Length <= second.Length)
            {
                for (int x = 0; x < first.Length; x++)
                {
                    if (first[x] != second[x]) return false;
                }
                return true;
            }
            return false;
        }


        /// <summary>
        /// Private variables.
        /// </summary>
        public readonly int fileVersion;
        public readonly int fileDbcount;
        private BinaryReader br;
        private long dataFilePosition; // Position in the file at which data begins.


        /// <summary>
        /// Constructor.
        /// </summary>
        /// <returns></returns>
        public ReadIPD(string dbFileName)
        {
            char[] buf = null;
            char[] fileHeader = {
                                    'I', 'n', 't', 'e', 'r', '@', 'c', 't', 'i', 'v', 'e', ' ',
                                    'P', 'a', 'g', 'e', 'r', ' ', 'B', 'a', 'c', 'k', 'u', 'p',
                                    '/', 'R', 'e', 's', 't', 'o', 'r', 'e', ' ', 'F', 'i', 'l',
                                    'e', '\n'
                                };

            br = new BinaryReader(
                File.Open(dbFileName, FileMode.Open)
                );

            // Check file header.
            buf = new char[38];
            br.Read(buf, 0, 38);

            if (!isDupliateCharArray(fileHeader, buf))
                return;

            // Get info about this file.
            // buf[0] = Version
            // buf[2] = # Databases.
            // buf[4] = DB seperator?!?
            buf = new char[4];
            br.Read(buf, 0, 4);
            fileVersion = buf[0];
            fileDbcount = buf[2];
        }


        /// <summary>
        /// Find the tables, or databases, inside the current file.
        /// </summary>
        /// <returns></returns>
        public string[] getDatabaseNames()
        {
            string[] databaseNameArray = new string[fileDbcount];
            char[] buf = new char[2];

            for (int i = 0; i < fileDbcount; i++)
            {
                // Get record length (I guess?)
                br.Read(buf, 0, 2);
                int databaseLen = buf[0];

                // Get the name of this database.
                char[] buf2 = new char[databaseLen];
                br.Read(buf2, 0, databaseLen);
                databaseNameArray[i] = new string(buf2);
            }

            dataFilePosition = br.BaseStream.Position;

            return databaseNameArray;
        }


        /// <summary>
        /// Extract records of a given type from the database and pass over to a handler function.
        /// </summary>
        public List<object> getDatabaseRecords(int requiredDatabaseId, RecordTypes type)
        {
            List<object> databaseRecords = new List<object>();

            // Loop whilst we have data to read.
            while (br.PeekChar() != -1)
            {
                // Get record ID.
                byte[] bBuf = br.ReadBytes(6);
                int databaseId = bBuf[0];

                // Get record length.
                ulong recordLen = BitConverter.ToUInt32(bBuf, 2);

                // Get record data.
                byte[] bBuf2 = br.ReadBytes(Convert.ToInt32(recordLen));

                // Handle the record, split it into fields and then pass it ober to a hander method
                // that can handle this record type.
                if (databaseId == requiredDatabaseId)
                {
                    IPD_Record thisRecord;
                    thisRecord.DatabaseRecordhandle = bBuf2[0];
                    thisRecord.DatabaseVersion = BitConverter.ToUInt16(bBuf2, 1);
                    thisRecord.RecordUiniqueId = BitConverter.ToUInt32(bBuf2, 3);

                    thisRecord.RecordData = new byte[bBuf2.Length - 5];
                    Array.Copy(bBuf2, 5, thisRecord.RecordData, 0, bBuf2.Length - 5);

                    switch (type)
                    {
                        case RecordTypes.SMS:
                            databaseRecords.Add(extractSMSMessage(thisRecord));
                            break;
                    }
                }
            }

            // Reset position in database to start of data after reading these records.
            dataFilePosition = br.BaseStream.Position;

            return databaseRecords;
        }


        /// <summary>
        /// Extracts an SMS message in IPD_SMSMESSAGE format from an IPD_Record struct.
        /// </summary>
        /// <param name="rawRecord"></param>
        private IPD_SMSRecord extractSMSMessage(IPD_Record record)
        {
            IPD_SMSRecord smsMessage = new IPD_SMSRecord();

            // Iterate over each field in this record.
            for (int i = 2; i < record.RecordData.Length; i++)
            {
                ushort fieldLength = BitConverter.ToUInt16(record.RecordData, i);
                int fieldType = record.RecordData[i + 2];
                byte[] fieldData = new byte[fieldLength]; // Byte array length described by fieldLength.
                Array.Copy(record.RecordData, i + 3, fieldData, 0, fieldLength);

                smsMessage.addField(fieldType, fieldLength, fieldData);

                i += fieldLength + 2;
            }
            return smsMessage;
        }


        /// <summary>
        /// Closes the filehandle associated with ByteReader.
        /// </summary>
        public void Dispose()
        {
            br.Close();
            dataFilePosition = -1;
        }

    }
}

