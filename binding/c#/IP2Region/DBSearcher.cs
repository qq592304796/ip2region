//*******************************
// Create By Rocher Kong 
// Github https://github.com/RocherKong
// Date 2018.02.09
//*******************************
using System;
using System.IO;
using System.Text;

namespace IP2Region
{
    public class DbSearcher
    {
        public static int BTREE_ALGORITHM = 1;
        public static int BINARY_ALGORITHM = 2;
        public static int MEMORY_ALGORITYM = 3;

        /**
         * db config
        */
        private DbConfig dbConfig = null;

        /**
         * db file access handler
        */
        private FileStream raf = null;

        /**
         * header blocks buffer 
        */
        private long[] HeaderSip = null;
        private int[] HeaderPtr = null;
        private int headerLength;

        /**
         * super blocks info 
        */
        private long firstIndexPtr = 0;
        private long lastIndexPtr = 0;
        private int totalIndexBlocks = 0;

        /**
         * for memory mode
         * the original db binary string
        */
        private byte[] dbBinStr = null;

        /**
         * construct class
         * 
         * @param   bdConfig
         * @param   dbFile
         * @throws  FileNotFoundException 
        */
        public DbSearcher(DbConfig dbConfig, String dbFile)
        {
            this.dbConfig = dbConfig;
            raf = new FileStream(dbFile, FileMode.Open, FileAccess.Read);
        }

        /**
         * get the region with a int ip address with memory binary search algorithm
         *
         * @param   ip
         * @throws  IOException
        */
        public DataBlock MemorySearch(long ip)
        {
            int blen = IndexBlock.GetIndexBlockLength();
            if (dbBinStr == null)
            {
                dbBinStr = new byte[(int)raf.Length];
                raf.Seek(0L, SeekOrigin.Begin);
                raf.Read(dbBinStr, 0, dbBinStr.Length);

                //initialize the global vars
                firstIndexPtr = Util.getIntLong(dbBinStr, 0);
                lastIndexPtr = Util.getIntLong(dbBinStr, 4);
                totalIndexBlocks = (int)((lastIndexPtr - firstIndexPtr) / blen) + 1;
            }

            //search the index blocks to define the data
            int l = 0, h = totalIndexBlocks;
            long sip, eip, dataptr = 0;
            while (l <= h)
            {
                int m = (l + h) >> 1;
                int p = (int)(firstIndexPtr + m * blen);

                sip = Util.getIntLong(dbBinStr, p);
                if (ip < sip)
                {
                    h = m - 1;
                }
                else
                {
                    eip = Util.getIntLong(dbBinStr, p + 4);
                    if (ip > eip)
                    {
                        l = m + 1;
                    }
                    else
                    {
                        dataptr = Util.getIntLong(dbBinStr, p + 8);
                        break;
                    }
                }
            }

            //not matched
            if (dataptr == 0) return null;

            //get the data
            int dataLen = (int)((dataptr >> 24) & 0xFF);
            int dataPtr = (int)((dataptr & 0x00FFFFFF));
            int city_id = (int)Util.getIntLong(dbBinStr, dataPtr);
            String region = System.Text.Encoding.UTF8.GetString(dbBinStr, dataPtr + 4, dataLen - 4);//new String(dbBinStr, dataPtr + 4, dataLen - 4, Encoding.UTF8);

            return new DataBlock(city_id, region, dataPtr);
        }

        /**
         * get the region throught the ip address with memory binary search algorithm
         * 
         * @param   ip
         * @return  DataBlock
         * @throws  IOException 
*/
        public DataBlock MemorySearch(String ip)
        {
            return MemorySearch(Util.ip2long(ip));
        }


        /**
         * get by index ptr
         * 
         * @param   indexPtr
         * @throws  IOException 
*/
        public DataBlock GetByIndexPtr(long ptr)
        {
            raf.Seek(ptr, SeekOrigin.Begin);
            byte[]
            buffer = new byte[12];
            raf.Read(buffer, 0, buffer.Length);
            //long startIp = Util.getIntLong(buffer, 0);
            //long endIp = Util.getIntLong(buffer, 4);
            long extra = Util.getIntLong(buffer, 8);

            int dataLen = (int)((extra >> 24) & 0xFF);
            int dataPtr = (int)((extra & 0x00FFFFFF));

            raf.Seek(dataPtr, SeekOrigin.Begin);
            byte[] data = new byte[dataLen];
            raf.Read(data, 0, data.Length);

            int city_id = (int)Util.getIntLong(data, 0);
            String region = Encoding.UTF8.GetString(data, 4, data.Length - 4);
            //new String(data, 4, data.Length - 4, "UTF-8");

            return new DataBlock(city_id, region, dataPtr);
        }

        /**
         * get the region with a int ip address with b-tree algorithm
         * 
         * @param   ip
         * @throws  IOException 
*/
        public DataBlock BtreeSearch(long ip)
        {
            //check and load the header
            if (HeaderSip == null)
            {
                raf.Seek(8L, SeekOrigin.Begin);    //pass the super block
                                                   //byte[] b = new byte[dbConfig.getTotalHeaderSize()];
                byte[] b = new byte[4096];
                raf.Read(b, 0, b.Length);

                //fill the header
                int len = b.Length >> 3, idx = 0;  //b.lenght / 8
                HeaderSip = new long[len];
                HeaderPtr = new int[len];
                long startIp, dataPtrTemp;
                for (int i = 0; i < b.Length; i += 8)
                {
                    startIp = Util.getIntLong(b, i);
                    dataPtrTemp = Util.getIntLong(b, i + 4);
                    if (dataPtrTemp == 0) break;

                    HeaderSip[idx] = startIp;
                    HeaderPtr[idx] = (int)dataPtrTemp;
                    idx++;
                }

                headerLength = idx;
            }

            //1. define the index block with the binary search
            if (ip == HeaderSip[0])
            {
                return GetByIndexPtr(HeaderPtr[0]);
            }
            else if (ip == HeaderSip[headerLength - 1])
            {
                return GetByIndexPtr(HeaderPtr[headerLength - 1]);
            }

            int l = 0, h = headerLength, sptr = 0, eptr = 0;
            while (l <= h)
            {
                int m = (l + h) >> 1;

                //perfetc matched, just return it
                if (ip == HeaderSip[m])
                {
                    if (m > 0)
                    {
                        sptr = HeaderPtr[m - 1];
                        eptr = HeaderPtr[m];
                    }
                    else
                    {
                        sptr = HeaderPtr[m];
                        eptr = HeaderPtr[m + 1];
                    }

                    break;
                }

                //less then the middle value
                if (ip < HeaderSip[m])
                {
                    if (m == 0)
                    {
                        sptr = HeaderPtr[m];
                        eptr = HeaderPtr[m + 1];
                        break;
                    }
                    else if (ip > HeaderSip[m - 1])
                    {
                        sptr = HeaderPtr[m - 1];
                        eptr = HeaderPtr[m];
                        break;
                    }
                    h = m - 1;
                }
                else
                {
                    if (m == headerLength - 1)
                    {
                        sptr = HeaderPtr[m - 1];
                        eptr = HeaderPtr[m];
                        break;
                    }
                    else if (ip <= HeaderSip[m + 1])
                    {
                        sptr = HeaderPtr[m];
                        eptr = HeaderPtr[m + 1];
                        break;
                    }
                    l = m + 1;
                }
            }

            //match nothing just stop it
            if (sptr == 0) return null;

            //2. search the index blocks to define the data
            int blockLen = eptr - sptr, blen = IndexBlock.GetIndexBlockLength();
            byte[]
            iBuffer = new byte[blockLen + blen];    //include the right border block
            raf.Seek(sptr, SeekOrigin.Begin);
            raf.Read(iBuffer, 0, iBuffer.Length);

            l = 0; h = blockLen / blen;
            long sip, eip, dataptr = 0;
            while (l <= h)
            {
                int m = (l + h) >> 1;
                int p = m * blen;
                sip = Util.getIntLong(iBuffer, p);
                if (ip < sip)
                {
                    h = m - 1;
                }
                else
                {
                    eip = Util.getIntLong(iBuffer, p + 4);
                    if (ip > eip)
                    {
                        l = m + 1;
                    }
                    else
                    {
                        dataptr = Util.getIntLong(iBuffer, p + 8);
                        break;
                    }
                }
            }

            //not matched
            if (dataptr == 0) return null;

            //3. get the data
            int dataLen = (int)((dataptr >> 24) & 0xFF);
            int dataPtr = (int)((dataptr & 0x00FFFFFF));

            raf.Seek(dataPtr, SeekOrigin.Begin);
            byte[] data = new byte[dataLen];
            raf.Read(data, 0, data.Length);

            int city_id = (int)Util.getIntLong(data, 0);
            String region = Encoding.UTF8.GetString(data, 4, data.Length - 4);// new String(data, 4, data.Length - 4, "UTF-8");

            return new DataBlock(city_id, region, dataPtr);
        }

        /**
         * get the region throught the ip address with b-tree search algorithm
         * 
         * @param   ip
         * @return  DataBlock
         * @throws  IOException 
        */
        public DataBlock BtreeSearch(String ip)
        {
            return BtreeSearch(Util.ip2long(ip));
        }

        /**
         * get the region with a int ip address with binary search algorithm
         * 
         * @param   ip
         * @throws  IOException 
*/
        public DataBlock BinarySearch(long ip)
        {
            int blen = IndexBlock.GetIndexBlockLength();
            if (totalIndexBlocks == 0)
            {
                raf.Seek(0L, SeekOrigin.Begin);
                byte[] superBytes = new byte[8];
                raf.Read(superBytes, 0, superBytes.Length);
                //initialize the global vars
                firstIndexPtr = Util.getIntLong(superBytes, 0);
                lastIndexPtr = Util.getIntLong(superBytes, 4);
                totalIndexBlocks = (int)((lastIndexPtr - firstIndexPtr) / blen) + 1;
            }

            //search the index blocks to define the data
            int l = 0, h = totalIndexBlocks;
            byte[]
            buffer = new byte[blen];
            long sip, eip, dataptr = 0;
            while (l <= h)
            {
                int m = (l + h) >> 1;
                raf.Seek(firstIndexPtr + m * blen, SeekOrigin.Begin);    //set the file pointer
                raf.Read(buffer, 0, buffer.Length);
                sip = Util.getIntLong(buffer, 0);
                if (ip < sip)
                {
                    h = m - 1;
                }
                else
                {
                    eip = Util.getIntLong(buffer, 4);
                    if (ip > eip)
                    {
                        l = m + 1;
                    }
                    else
                    {
                        dataptr = Util.getIntLong(buffer, 8);
                        break;
                    }
                }
            }

            //not matched
            if (dataptr == 0) return null;

            //get the data
            int dataLen = (int)((dataptr >> 24) & 0xFF);
            int dataPtr = (int)((dataptr & 0x00FFFFFF));

            raf.Seek(dataPtr, SeekOrigin.Begin);
            byte[] data = new byte[dataLen];
            raf.Read(data, 0, data.Length);

            int city_id = (int)Util.getIntLong(data, 0);
            String region = Encoding.UTF8.GetString(data, 4, data.Length - 4);//new String(data, 4, data.Length - 4, "UTF-8");

            return new DataBlock(city_id, region, dataPtr);
        }

        /**
         * get the region throught the ip address with binary search algorithm
         * 
         * @param   ip
         * @return  DataBlock
         * @throws  IOException 
        */
        public DataBlock BinarySearch(String ip)
        {
            return BinarySearch(Util.ip2long(ip));
        }

        /**
         * get the db config
         *
         * @return  DbConfig
*/
        public DbConfig GetDbConfig()
        {
            return dbConfig;
        }

        /**
         * close the db 
         * 
         * @throws IOException 
*/
        public void Close()
        {
            HeaderSip = null;    //let gc do its work
            HeaderPtr = null;
            dbBinStr = null;
            raf.Close();
        }

    }

}