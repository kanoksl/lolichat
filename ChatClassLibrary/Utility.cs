﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ChatClassLibrary
{
    public static class Utility
    {
        /// <summary>
        /// Convert a 32-bit integer to an array of 4 bytes (using big endian).
        /// </summary>
        /// <param name="value">Integer value to be converted.</param>
        /// <returns>A byte array of length 4.</returns>
        public static byte[] IntToBytes(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return bytes;
        }

        /// <summary>
        /// Convert the first 4 bytes of an array to a 32-bit integer.
        /// </summary>
        /// <param name="bytes">Byte array to convert.</param>
        /// <returns>A 32-bit integer.</returns>
        public static int BytesToInt(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            int value = BitConverter.ToInt32(bytes, 0);
            return value;
        }

        /// <summary>
        /// Convert a 64-bit integer to an array of 8 bytes (using big endian).
        /// </summary>
        /// <param name="value">Integer value to be converted.</param>
        /// <returns>A byte array of length 8.</returns>
        public static byte[] LongToBytes(long value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            return bytes;
        }

        /// <summary>
        /// Convert the first 8 bytes of an array to a 64-bit integer.
        /// </summary>
        /// <param name="bytes">Byte array to convert.</param>
        /// <returns>A 64-bit integer.</returns>
        public static long BytesToLong(byte[] bytes)
        {
            if (BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            long value = BitConverter.ToInt64(bytes, 0);
            return value;
        }

        //--------------------------------------------------------------------------------------//

        /// <summary>
        /// Concatenate two arrays of same type.
        /// </summary>
        /// <typeparam name="T">Can be any type.</typeparam>
        /// <param name="x">The first array.</param>
        /// <param name="y">The second array.</param>
        /// <returns>A new array resulted from concatenating y to x.</returns>
        public static T[] Concat<T>(T[] x, T[] y)
        {
            var z = new T[x.Length + y.Length];
            x.CopyTo(z, 0);
            y.CopyTo(z, x.Length);
            return z;
        }

        /// <summary>
        /// Concatenate multiple arrays of same type.
        /// </summary>
        /// <typeparam name="T">Can be any type.</typeparam>
        /// <param name="arr">Arrays to be concatenated.</param>
        /// <returns>A new array resulted from concatenating the input arrays in the given order.</returns>
        public static T[] Concat<T>(params T[][] arr)
        {
            int totalLength = 0;
            foreach (T[] a in arr) totalLength += a.Length;

            var z = new T[totalLength];
            int offset = 0;
            foreach (T[] a in arr)
            {
                a.CopyTo(z, offset);
                offset += a.Length;
            }
            return z;
        }

        public static T[] Slice<T>(T[] source, int index, int length)
        {
            var slice = new T[length];
            Array.Copy(source, index, slice, 0, length);
            return slice;
        }

        //--------------------------------------------------------------------------------------//

        /// <summary>
        /// Calculate the MD5 checksum of a given stream as a byte array.
        /// </summary>
        /// <param name="stream">The input to compute the hash code for.</param>
        /// <returns>A byte array of length 16 containing the hash.</returns>
        public static byte[] CalculateMD5(Stream stream)
        {
            using (var hasher = MD5.Create())
            {
                byte[] hash = hasher.ComputeHash(stream);
                return hash;
            }
        }

        /// <summary>
        /// Calculate the MD5 checksum of a file as a byte array.
        /// </summary>
        /// <param name="filePath">Path of the file.</param>
        /// <returns>A byte array of length 16 containing the hash.</returns>
        public static byte[] CalculateMD5(string filePath)
        {
            return CalculateMD5(File.OpenRead(filePath));
        }

        /// <summary>
        /// Convert an array of hash to standard-looking string.
        /// </summary>
        /// <param name="hash">Array returned from <code>ComputeHash()</code>.</param>
        /// <returns>A lowecase hexadecimal string of the hash value.</returns>
        public static string ToHashString(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        //--------------------------------------------------------------------------------------//
    }
}
