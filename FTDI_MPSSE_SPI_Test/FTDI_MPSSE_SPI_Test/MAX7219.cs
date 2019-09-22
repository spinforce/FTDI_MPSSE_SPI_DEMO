using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FTDI_MPSSE_SPI_Test
{
    class MAX7219
    {
        private byte[] Map7219;
        private byte[] Map7219c;
        private byte[] WriteBuffer;
        private uint WritePointer;
        private byte Scanlimit;
        public delegate void Write(byte[] DataBuffer, ref uint BufferPointer, byte Hi, byte Lo);

        public Write WriteCallback;



        public MAX7219(byte[] b, Write callback)
        {
           Map7219c = new byte[] { 1<< 7 }; //Punkt
           Map7219 = new byte[] {
               ( 1<<1 | 1 << 2 | 1 << 3 | 1 << 4 | 1 << 5 | 1 << 6),        //0
               (1 << 4 | 1 << 5),                                           //1
               (1 << 0 | 1 << 2 | 1 << 3 | 1 << 5 | 1 << 6),                //2
               (1 << 0 | 1 << 3 | 1 << 4 | 1 << 5 | 1 << 6),                //3
               (1 << 0 | 1 << 1 | 1 << 4 | 1 << 5),                         //4
               (1 << 0 | 1 << 1 | 1 << 3 | 1 << 4 | 1 << 6),                //5
               (1 << 0 | 1 << 1 | 1 << 2 | 1 << 3 | 1 << 4 | 1 << 6 ),      //6
               (1 << 1 | 1 << 4 | 1 << 5 | 1 << 6),0x7F,                    //7,8
               (1 << 0 | 1 << 1 | 1 << 3 | 1 << 4 | 1 << 5 | 1 << 6 ),      //9
               (1 << 0 | 1 << 1 | 1 << 2 | 1 << 4 | 1 << 5 | 1 << 6 ),      //A
               (1 << 0 | 1 << 1 | 1 << 2 | 1 << 3 | 1 << 4),                //b
               (1 << 1 | 1 << 2 | 1 << 3 | 1 << 6),                         //C
               (1 << 0 | 1 << 2 | 1 << 3 | 1 << 4 | 1 << 5),                //D
               (1 << 0 | 1 << 1 | 1 << 2 | 1 << 3 | 1 << 6),                //E
               (1 << 0 | 1 << 1 | 1 << 2 | 1 << 6),                         //F
           };

            WriteBuffer = b;
            WriteCallback = new Write(callback);
           
        }
        /// <summary>
        /// Init MAX7219
        /// </summary>
        /// <param name="scl">Number of Digits</param>
        /// <returns></returns>
        public uint Init(byte scl)
        {
            WritePointer = 0;
            Scanlimit = (byte)(scl-1);

            WriteCallback(WriteBuffer, ref WritePointer, 0x0c, 0x00);//Shutdown
            WriteCallback(WriteBuffer, ref WritePointer, 0x0c, 0x01);//Shutdown 1=ON	
            WriteCallback(WriteBuffer, ref WritePointer, 0x0a, 0x02);//Intensity
            WriteCallback(WriteBuffer, ref WritePointer, 0x0f, 0x00);//Test Normal
            WriteCallback(WriteBuffer, ref WritePointer, 0x0b, Scanlimit);//Scan Limit 8 Segments	
            WriteCallback(WriteBuffer, ref WritePointer, 0x09, 0x00);//Decode Mode - No Decode

            return WritePointer;
        }

        public uint Clr()
        {
            WritePointer = 0;

            for (byte ix = 1; ix < Scanlimit + 2; ix++)
            {
                WriteCallback(WriteBuffer, ref WritePointer, ix, 0x00);
            }

            return WritePointer;
        }


        /// <summary>
        /// Fill Buffer with Decimal Data
        /// </summary>
        /// <param name="data">Hex Values</param>
        /// <returns></returns>
        public uint WriteHex(UInt32 data)
        {
            WritePointer = 0;
            for (byte ix = 1; ix < Scanlimit+2; ix++)
            {
                WriteCallback(WriteBuffer, ref WritePointer, ix, Map7219[(byte)(data & 0xF)]);
                data >>= 4;
            }

            return WritePointer;
        }

        /// <summary>
        /// Fill Buffer with Decimal Data
        /// </summary>
        /// <param name="data">Decimal Value</param>
        /// <param name="d">Point at Digit</param>
        /// <returns></returns>
        public uint WriteDec(UInt32 data, byte d=0)
        {
            WritePointer = 0;
            for (byte ix = 0; ix < Scanlimit + 1; ix++)
            {
                WriteCallback(WriteBuffer, ref WritePointer, (byte)(ix + 1), (byte)(Map7219[(byte)(data % 10)] | (d & (1<<ix)) << 7-ix));
                data /= 10;
            }
            return WritePointer;

        }


    }
}
