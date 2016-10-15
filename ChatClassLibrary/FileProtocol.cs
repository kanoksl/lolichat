﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace ChatClassLibrary
{
    public static class FileProtocol
    {
        /// <summary>
        /// Port that server uses to listen for file upload/download requests.
        /// </summary>
        public static int FtpListeningPort => 60001;

        /// <summary>
        /// Buffer size used in file sending/receiving operations.
        /// </summary>
        public static int FtpBufferSize => 8192;

        /// <summary>
        /// Text encoding for all file-transfer-related operations.
        /// </summary>
        public static Encoding TextEncoding => Encoding.UTF8;

        //--------------------------------------------------------------------------------------//

        public static bool SendFile(string filePath, IPEndPoint receiverEP,
            IProgress<double> progress = null)
        {
            TcpClient receiverSocket = new TcpClient();
            NetworkStream receiverStream = null;

            try
            {
                _Log("Connecting to receiver at {0}", receiverEP);

                receiverSocket.Connect(receiverEP);
                receiverStream = receiverSocket.GetStream();

                FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                int packetCount = (int) Math.Ceiling((double) fileStream.Length / FtpBufferSize);
                long fileLength = fileStream.Length;
                long bytesSent = 0;

                // Step 1: Send the information about the file as the first packet.

                byte[] fileName = new byte[256];  // Max file name length = 255 bytes.
                TextEncoding.GetBytes(Path.GetFileName(filePath)).CopyTo(fileName, 0);
                byte[] fileSize = Utility.LongToBytes(fileLength);  // 8 bytes.
                byte[] fileHash = Utility.CalculateMD5(filePath);  // 16 bytes.
                byte[] firstPacket = Utility.Concat(fileName, fileSize, fileHash); // 280 bytes.

                _Log("Sending the file information packet...");
                _Log(" FileName = " + Path.GetFileName(filePath));
                _Log(" FileSize = {0} bytes", fileLength);
                _Log(" FileHash = " + Utility.ToHashString(fileHash));

                receiverStream.Write(firstPacket, 0, firstPacket.Length);

                // Step 2: Receive the confirmation from the receiver.

                byte[] response = new byte[256];
                receiverStream.Read(response, 0, response.Length);
                bool receiverAccept = response[0] == 1;
                if (!receiverAccept)
                {
                    string message = TextEncoding.GetString(response, 1, response.Length - 1);
                    _Log("Receiver rejected file transfer request. Reason: " + message);
                    return false;
                }

                // Step 3: Start the actual file transfer.

                progress?.Report(0);  // 0% complete.

                _Log("Sending a file of length = {0} bytes ({1} packets)", fileLength, packetCount);
                _Log(" ({0})", filePath);

                for (int i = 0; i < packetCount; i++)
                {
                    int bytesToSend = (int) Math.Min(fileLength - bytesSent, FtpBufferSize);
                    byte[] sendBytes = new byte[bytesToSend];

                    fileStream.Read(sendBytes, 0, bytesToSend);
                    _Log(" |- read {0} bytes from file", bytesToSend);
                    receiverStream.Write(sendBytes, 0, bytesToSend);
                    _Log(" |- sent {0} bytes to receiver", bytesToSend);

                    bytesSent += bytesToSend;

                    progress?.Report((double) bytesSent / fileLength * 100);
                }

                progress?.Report(100);  // Finished transfer; 100% complete.
                _Log("Finished sending {0} bytes ({1} packets)", fileLength, packetCount);

                fileStream.Close();

                return true;
            }
            catch (Exception ex)
            {
                _Log("Exception caught: " + ex.Message);
                return false;
            }
            finally
            {
                receiverStream?.Close();
                receiverSocket?.Close();
            }
        }

        public static bool ReceiveFile(TcpListener listenerSocket,
            IProgress<double> progress = null)
        {
            TcpClient senderSocket = listenerSocket.AcceptTcpClient();
            NetworkStream stream = senderSocket.GetStream();

            // Step 1: Receive information about the file.
            byte[] fileInfo = new byte[280];
            stream.Read(fileInfo, 0, fileInfo.Length);
            string fileName = TextEncoding.GetString(fileInfo, 0, 256).Trim('\0');
            long fileLength = Utility.BytesToLong(Utility.Slice(fileInfo, 256, 8));
            byte[] fileHash = Utility.Slice(fileInfo, 264, 16);

            _Log("Received a file information packet");
            _Log(" FileName = " + fileName);
            _Log(" FileSize = {0} bytes", fileLength);
            _Log(" FileHash = " + Utility.ToHashString(fileHash));

            // Step 2: Respond whether to accept or reject the file.

            byte[] response = new byte[256];
            bool accept = true;  // TODO: conditions for rejecting file upload

            if (accept)
            {
                response[0] = 1;
                stream.Write(response, 0, response.Length);
            }
            else
            {
                response[0] = 0;
                string message = "I don't want this file.";
                TextEncoding.GetBytes(message, 0, message.Length, response, 1);
                stream.Write(response, 0, response.Length);

                return false;
            }

            // Step 3: Start the actual file transfer.

            progress?.Report(0);  // 0% complete.

            string saveFilePath = @"D:\tmp\ftp\" + fileName;  // TODO: configurable save path

            _Log("Receiving a file of length = {0} bytes", fileLength);
            _Log(" (Save to {0})", saveFilePath);

            FileStream fileStream = new FileStream(saveFilePath, FileMode.OpenOrCreate, FileAccess.Write);
            long readLength = 0;
            int packetCount = 0;

            while (readLength < fileLength)
            {
                int bytesToRead = (int) Math.Min(FtpBufferSize, fileLength - readLength);
                byte[] readBytes = new byte[bytesToRead];

                int bytesRead = stream.Read(readBytes, 0, bytesToRead);
                fileStream.Write(readBytes, 0, bytesRead);

                readLength += bytesToRead;
                packetCount += 1;

                progress?.Report((double) readLength / fileLength * 100);
            }

            progress?.Report(100);  // Finished transfer; 100% complete.
            _Log("Finished receiving {0} bytes ({1} packets)", fileLength, packetCount);

            fileStream.Close();
            stream.Close();
            senderSocket.Close();

            return true;
        }

        //--------------------------------------------------------------------------------------//

        private static void _Log(string message, params object[] args)
        {
            Console.WriteLine("[file_protocol] " + message, args);
        }
    }
}
