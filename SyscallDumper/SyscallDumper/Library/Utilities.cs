﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace SyscallDumper.Library
{
    internal class Utilities
    {
        public static Dictionary<string, int> DumpSyscallNumber(
            string filePath)
        {
            var results = new Dictionary<string, int>();
            var rgx = new Regex(@"^Nt\S+$");
            var fullPath = Path.GetFullPath(filePath);
            string imageName;
            int syscallNumber;
            Dictionary<string, IntPtr> exports;

            if (!File.Exists(fullPath))
            {
                Console.WriteLine("[-] {0} does not exists.", fullPath);

                return results;
            }

            try
            {
                Console.WriteLine("[>] Loading {0}.", fullPath);

                using (var pe = new PeFile(fullPath))
                {
                    imageName = pe.GetExportImageName();

                    Console.WriteLine("[+] {0} is loaded successfully.", fullPath);
                    Console.WriteLine("    [*] Architecture : {0}", pe.Architecture);
                    Console.WriteLine("    [*] Image Name   : {0}", imageName);

                    if (!Helpers.CompareStringIgnoreCase(imageName, "ntdll.dll") &&
                        !Helpers.CompareStringIgnoreCase(imageName, "win32u.dll"))
                    {
                        Console.WriteLine("[-] Loaded file is not ntdll.dll or win32u.dll.");

                        return results;
                    }

                    exports = pe.GetExports();

                    foreach (var entry in exports)
                    {
                        if (!rgx.IsMatch(entry.Key))
                            continue;

                        if (pe.Architecture == PeFile.IMAGE_FILE_MACHINE.I386)
                        {
                            if (pe.ReadByte(entry.Value) == 0xB8) // mov eax, 0x????
                            {
                                syscallNumber = pe.ReadInt32(entry.Value, 1);
                                results.Add(entry.Key, syscallNumber);
                            }
                        }
                        else if (pe.Architecture == PeFile.IMAGE_FILE_MACHINE.AMD64)
                        {
                            if (pe.SearchBytes(
                                entry.Value,
                                0x20,
                                new byte[] { 0x0F, 0x05 }).Length > 0) // syscall
                            {
                                if ((uint)pe.ReadInt32(entry.Value) == 0xB8D18B4C) // mov r10, rcx; mov eax, 0x???? 
                                {
                                    syscallNumber = pe.ReadInt32(entry.Value, 4);
                                    results.Add(entry.Key, syscallNumber);
                                }
                            }
                        }
                        else if (pe.Architecture == PeFile.IMAGE_FILE_MACHINE.ARM64)
                        {
                            if (((uint)pe.ReadInt32(entry.Value) & 0xFFE0001F) == 0xD4000001) // svc #0x????
                            {
                                syscallNumber = (pe.ReadInt32(entry.Value) >> 5) & 0x0000FFFF; // Decode svc instruction
                                results.Add(entry.Key, syscallNumber);
                            }
                        }
                        else
                        {
                            throw new InvalidDataException("Unsupported architecture.");
                        }
                    }
                }
            }
            catch (InvalidDataException ex)
            {
                Console.WriteLine("[!] {0}\n", ex.Message);

                return results;
            }

            if (results.Count > 0)
            {
                Console.WriteLine("[+] Got {0} syscall(s).", results.Count);
            }
            else
            {
                Console.WriteLine("[-] No syscall.");
            }

            return results;
        }
    }
}
