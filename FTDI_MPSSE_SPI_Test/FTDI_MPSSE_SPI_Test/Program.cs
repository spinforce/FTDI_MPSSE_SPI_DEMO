using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using FTD2XX_NET;

namespace FTDI_MPSSE_SPI_Test
{
    class Program
    {
        static void Main(string[] args)
        {

            FTDI ftdi = new FTDI();
            MAX7219 Display;

            uint devcount = 0;
            ftdi.GetNumberOfDevices(ref devcount);

            if (devcount > 0)
            {
                byte[] TransferBuffer = new byte[88];
                uint NumBytesToTransfer; 
                uint NumBytesTransfered = 0;

                Display = new MAX7219(TransferBuffer, WriteOut);

                //FTDI AN_108 3.8 Clock Divisor
                uint dwClockDivisor = 29; //Value of clock divisor, SCL Frequency = 60/((1+29)*2) (MHz) = 1Mhz

                FTDI.FT_DEVICE_INFO_NODE[] devices = new FTDI.FT_DEVICE_INFO_NODE[devcount];

                string Buffer;

                FTDI.FT_STATUS s = ftdi.GetDeviceList(devices);

                for (uint ix = 0; ix < devcount; ix++)
                {


                    ftdi.OpenBySerialNumber(devices[ix].SerialNumber);

                    ftdi.GetCOMPort(out Buffer);
                    Console.WriteLine(Buffer);


                    ftdi.GetDescription(out Buffer);
                    Console.WriteLine(Buffer);


                    //FTDI Set Mode MPSSE
                    s = ftdi.SetBitMode(0, FTDI.FT_BIT_MODES.FT_BIT_MODE_RESET);
                    s |= ftdi.SetBitMode(0, FTDI.FT_BIT_MODES.FT_BIT_MODE_MPSSE);

                    if (s != FTDI.FT_STATUS.FT_OK)
                    {
                        Console.WriteLine("Fehler SetBitMode");
                        ftdi.Close();
                        return;
                    }

                    //FTDI Sync MPSSE (Check) FTDI_AN114 Page 10
                    if (Sync_MPSSE(ref ftdi, TransferBuffer) != FTDI.FT_STATUS.FT_OK)
                    {
                        Console.WriteLine("Fehler Sync MPSSE");
                        ftdi.Close();
                        return;
                    }


                    //Init FTDI SPI  See FTDI AN_108
                    NumBytesToTransfer = 0;
                    TransferBuffer[NumBytesToTransfer++] = 0x8a; // Disable Clock divide /5
                    TransferBuffer[NumBytesToTransfer++] = 0x97; // Disable adaptiv clockink
                    TransferBuffer[NumBytesToTransfer++] = 0x8d; // Disables 3 phase data clocking

                    // I think is not nesacery
                    //TransferBuffer[NumBytesToTransfer++] = 0x80;
                    //TransferBuffer[NumBytesToTransfer++] = 0x08;
                    //TransferBuffer[NumBytesToTransfer++] = 0x0b;

                    TransferBuffer[NumBytesToTransfer++] = 0x86; //3.8 Clock Divisor
                    TransferBuffer[NumBytesToTransfer++] = (byte)(dwClockDivisor & 0xff);
                    TransferBuffer[NumBytesToTransfer++] = (byte)(dwClockDivisor >> 8);

                    s = ftdi.Write(TransferBuffer, NumBytesToTransfer, ref NumBytesTransfered);
                    NumBytesToTransfer = 0;
                    Thread.Sleep(20);


                    TransferBuffer[NumBytesToTransfer++] = 0x85; // Disable loopback
                    s |= ftdi.Write(TransferBuffer, NumBytesToTransfer, ref NumBytesTransfered);
                    NumBytesToTransfer = 0;
                    if (s != FTDI.FT_STATUS.FT_OK)
                    {
                        Console.WriteLine("SPI Init Fehler");
                        ftdi.Close();
                        return;
                    }
                    Console.WriteLine("SPI Init OK");

                    Console.WriteLine("Press ESC to Exit");

                    //Init 7219
                    s = ftdi.Write(TransferBuffer, Display.Init(8), ref NumBytesTransfered);

                    UInt32 count = 0;

                    while (true)
                    {
                        //Data     
                        s |= ftdi.Write(TransferBuffer, Display.WriteDec(count,(byte)(1<<((byte)(count++%8)))), ref NumBytesTransfered);

                        Console.WriteLine("SPI {0} Bytes Write", NumBytesTransfered);

                        Thread.Sleep(100);  

                        if (Console.KeyAvailable && (Console.ReadKey(true)).Key == ConsoleKey.Escape) break;
                    }



                    s |= ftdi.Write(TransferBuffer, Display.Clr(), ref NumBytesTransfered);


                    if (s != FTDI.FT_STATUS.FT_OK)
                    {
                        Console.WriteLine("SPI Fehler Write Data");
                        ftdi.Close();
                        return;
                    }

                    ftdi.Close();
                }
            }
            else
            {
                Console.WriteLine("Kein FTDI gefunden :-(");
            }


        }

        static FTDI.FT_STATUS Sync_MPSSE(ref FTDI ftdi, byte[] TransferBuffer)
        {


            /*
             See FTDI AN_114 Page 10-11 Synchronize the MPSSE interface by sending bad command ＆xAA＊
             For Check MPSSE is sucsessfully running send a bad obcode (0xaa)
             The request must be 0xfa and the bad obcode
             In the AN make it at twice.
             Im too lazy for this!
              */

            uint NumBytesToTransfer, TxRxQ = 0;
            uint NumBytesTransfered = 0;
            FTDI.FT_STATUS s;

            NumBytesToTransfer = 0;
            TransferBuffer[NumBytesToTransfer++] = 0xaa;
            ftdi.Write(TransferBuffer, NumBytesToTransfer, ref NumBytesTransfered);

            do
            {
                Thread.Sleep(5);
                ftdi.GetTxBytesWaiting(ref TxRxQ);

            } while (TxRxQ != 0);

            Thread.Sleep(20);

            s = ftdi.GetRxBytesAvailable(ref NumBytesToTransfer);

            if (s == FTDI.FT_STATUS.FT_OK)
            {
                ftdi.Read(TransferBuffer, NumBytesToTransfer, ref NumBytesTransfered);
                //if we get 0xfa and 0xaa MPSSE should be working
                if (TransferBuffer[0] != 0xfa || TransferBuffer[1] != 0xaa) return FTDI.FT_STATUS.FT_OTHER_ERROR;
            }
            else
            {
                return FTDI.FT_STATUS.FT_OTHER_ERROR;

            }

            return FTDI.FT_STATUS.FT_OK;

        }



        static void WriteOut(byte[] DataBuffer,ref uint ToTransfer,byte Hi,byte Lo)
        {
            /*
            See FTDI AN_108

            2.1 Data Bit definiton
            Bit0 = Clock
            Bit1 = DO
            Bit2 = DI
            Bit3 = CS
            */
            // Set CS Enable = Lo
            DataBuffer[ToTransfer++] = 0x80;    // 3.6 Set IOs
            DataBuffer[ToTransfer++] = 0x01;    // Clock start Hi
            DataBuffer[ToTransfer++] = 0x0b;    //3.6 Direction 1011

            DataBuffer[ToTransfer++] = 0x10;    //3.3 Obcode 0x10 Out, Byte, on Pos edge
            DataBuffer[ToTransfer++] = 2 - 1;   //Length of Data 0 = 1Byte LoByte
            DataBuffer[ToTransfer++] = 0;       //HiBite

            DataBuffer[ToTransfer++] = Hi;      // 2 Byte Data
            DataBuffer[ToTransfer++] = Lo;

            // Set CS Disable = Hi
            DataBuffer[ToTransfer++] = 0x80;     // 3.6 Set IOs
            DataBuffer[ToTransfer++] = 0x08;    //1000
            DataBuffer[ToTransfer++] = 0x0b;    //3.6 Direction 1011

        }
    }
}
