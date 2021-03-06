﻿using System;
using System.Text;

namespace ChatClassLibrary.Protocols
{
    public enum MessageType
    {
        /// <summary>
        /// Control information.
        /// </summary>
        Control,
        /// <summary>
        /// Special message generated by the application.
        /// </summary>
        SystemMessage,
        /// <summary>
        /// Messages that the user typed and sent in a chatroom. (TargetId is a chatroom ID).
        /// </summary>
        UserGroupMessage,
        /// <summary>
        /// User messages sent between two users. (TargetId is another client's ID).
        /// </summary>
        UserPrivateMessage
    }
    
    public enum ControlInfo
    {
        None,                     // Data message, not control message.

        ClientRequestConnection,  // The client wants to connect to server.
        ConnectionAccepted,       // The server accepted connection.
        ConnectionRejected,       // The server rejected connection.

        RequestFileUpload,        // The client wants to upload a file to server. Currently not used.
        RequestFileDownload,      // The client wants to download a file from server.
        RequestFileRemove,        // The client wants to delete a file from server.
        FileAvailable,            // A file has been uploaded. Message contains file info.
        FtpPortOpened,            // Tell the uploader to connect to this port.
        
        ListOfClients,            // Message containing a list of clients in a chatroom.
        ListOfChatrooms,          // Message containing a list of chatrooms in the server.
        ListOfFiles,              // Message containing a list of files in a chatroom (or private chat).

        RequestJoinChatroom,
        RequestLeaveChatroom,
        RequestCreateChatroom,

        ClientJoinedChatroom,     // A new client has joined a chatroom.
        ClientLeftChatroom        // A client has left a chatroom.
    }

    // The structure of a message packet is:
    //
    //    |------------------ Fixed-length Header (45 bytes) --------------| |-- Data --|
    //
    //    2-bit Message Type
    //     |    6-bit Control Code                      Time Sent (binary DateTime)
    //     |     |                                       |
    //   [ 1 1 , 0 0 0 0 0 0 | <16-byte> | <16-byte> | <8-byte> | <4-byte> || <variable> ]
    //                           |           |                      |           |
    //                  Sender GUID          |     Data Length (int32)          |
    //                     Target chatroom GUID              Data (actual message, optional)
    //
    //
    // The first byte is a combination of Message Type and Control Info.
    // In case of server-generated messages, the Sender GUID field is set to a special value, e.g. all zeroes.
    // Time Sent is in universal time binary, using DateTime.ToUniversalTime().ToBinary().
    // The Data Length and Data fields can be missing (for some types of control message).
    // Packet length must be at least 45 bytes (header-only).
    
    public class Message
    {
        /// <summary>
        /// Size of fixed-length header of all messages = 45 bytes.
        /// </summary>
        public static int HeaderLength => 1 + 16 + 16 + 8 + 4;

        //--------------------------------------------------------------------------------------//

        #region Public Properties

        /// <summary>
        /// (2-bit) The type of the message.
        /// </summary>
        public MessageType Type { get; set; }

        /// <summary>
        /// (6-bit) The control code, if the message is not a data message.
        /// </summary>
        public ControlInfo ControlInfo { get; set; }

        /// <summary>
        /// (16-byte GUID) The client who send the message.
        /// </summary>
        public Guid SenderId { get; set; }

        /// <summary>
        /// (16-byte GUID) Chatroom that the message is sent to.
        /// </summary>
        public Guid TargetId { get; set; }

        /// <summary>
        /// (8-byte) The time the message was sent (right before writing on a network stream).
        /// </summary>
        public DateTime TimeSent { get; set; }

        /// <summary>
        /// (not included in packet) The time the message was read by the receiver.
        /// </summary>
        public DateTime TimeReceived { get; set; }

        /// <summary>
        /// (variable-length) The content of the message.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Message is considered invalid if the first byte is 0, which normally would not happen.
        /// (Can occur when reading a message from an empty/disconnected stream).
        /// </summary>
        public bool IsValid
            => !(this.Type == MessageType.Control && this.ControlInfo == ControlInfo.None);

        #endregion

        //--------------------------------------------------------------------------------------//

        /// <summary>
        /// Convert a Message object into a byte array (to be sent over a network).
        /// Note: the Time Sent field is set to the time when the function is called. Can be
        /// updated later before sending with Message.UpdatePacketTimeStamp().
        /// </summary>
        /// <returns>A byte array representing the Message object.</returns>
        public byte[] BuildPacket()
        {
            byte[] packet = null;

            byte[] firstByte = { (byte) (((int) this.Type << 6) + (int) this.ControlInfo) };
            byte[] senderGuid = this.SenderId.ToByteArray();
            byte[] targetGuid = this.TargetId.ToByteArray();
            byte[] timeSent = Utility.ToByteArray(this.TimeSent.ToUniversalTime().ToBinary()); // Can be updated later.

            if (!string.IsNullOrEmpty(this.Text))
            {
                byte[] data = ProtocolSettings.TextEncoding.GetBytes(this.Text);
                byte[] dataLength = Utility.ToByteArray(data.Length);
                packet = Utility.Concat(firstByte, senderGuid, targetGuid, timeSent, dataLength, data);
            }
            else
            {
                byte[] dataLength = Utility.ToByteArray(0);
                packet = Utility.Concat(firstByte, senderGuid, targetGuid, timeSent, dataLength);
            }

            return packet;
        }

        /// <summary>
        /// Convert a byte array (read from a network stream) into a Message object.
        /// Note: this function does not check if the Text field of the packet is of the correct
        /// length as specified in the Data Length field. User can pass only the header of the
        /// packet here and then set the Text property themselves.
        /// </summary>
        /// <param name="packet">A byte array representing a Message object.</param>
        /// <returns>A Message object.</returns>
        public static Message FromPacket(byte[] packet)
        {
            if (packet.Length < Message.HeaderLength) return null;  // Incorrect packet bytes (too short).

            int messageType = packet[0] >> 6;  // The higher 2 bits.
            int controlCode = packet[0] & 0x3F;  // The lower 6 bits.

            Guid senderGuid = new Guid(Utility.Slice(packet, 1, 16));
            Guid targetGuid = new Guid(Utility.Slice(packet, 1 + 16, 16));

            long timeSentLong = Utility.BytesToInt64(packet, 1 + 16 + 16);
            DateTime timeSent = DateTime.FromBinary(timeSentLong).ToLocalTime();

            // All bytes after the header (if exist) are assumed to be part of the message text.
            string text = (packet.Length == Message.HeaderLength) ? null
                        : ProtocolSettings.TextEncoding.GetString(packet, Message.HeaderLength,
                                                                  packet.Length - Message.HeaderLength);
            return new Message
            {
                Type = (MessageType) messageType,
                ControlInfo = (ControlInfo) controlCode,
                SenderId = senderGuid,
                TargetId = targetGuid,
                TimeSent = timeSent,
                TimeReceived = DateTime.MinValue, // The receiver will set this.
                Text = text
            };
        }

        /// <summary>
        /// Update the Time Sent field of the byte packet to the specified value.
        /// </summary>
        /// <param name="packet">A byte array representing the Message object.</param>
        /// <param name="time">The time to be set in the Message's Time Sent field.</param>
        public static void UpdatePacketTimeStamp(byte[] packet, DateTime time)
        {
            byte[] timeSent = Utility.ToByteArray(time.ToUniversalTime().ToBinary());
            Array.Copy(timeSent, 0, packet, 33, 8); 
        }

        /// <summary>
        /// Update the Time Sent field of the byte packet to the current time.
        /// </summary>
        /// <param name="packet">A byte array representing the Message object.</param>
        public static void UpdatePacketTimeStamp(byte[] packet)
        {
            Message.UpdatePacketTimeStamp(packet, DateTime.Now);
        }

        //--------------------------------------------------------------------------------------//
        
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Message (T:{0}, CTRL:{1})\n", this.Type, this.ControlInfo);
            sb.Append("  - sender: ").Append(this.SenderId).AppendLine();
            sb.Append("  - target: ").Append(this.TargetId).AppendLine();
            sb.Append("  - time sent: ").Append(this.TimeSent).AppendLine();
            sb.Append("  - time recv: ").Append(this.TimeReceived).AppendLine();
            if (this.Text == null)
                return sb.ToString();
            sb.Append("  - content length (bytes): ").Append(
                ProtocolSettings.TextEncoding.GetByteCount(this.Text)).AppendLine();
            sb.Append("  - content: ---------------------------").AppendLine();
            sb.Append(this.Text).AppendLine();
            sb.Append("----------------------------------------").AppendLine();
            return sb.ToString();
        }
    }
}
