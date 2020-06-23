using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FVDetectScenes
{
    class Program
    {
        const string c_syntax =
@"Syntax: FVDetectScenes <filename>
   Filename should be a video file such as .mp4.
   Wildcards are acceptable.";

        static bool s_clSyntaxError;
        static string s_clFilePattern;

        static void Main(string[] args)
        {
            try
            {
                ParseCommandLine(args);
                if (s_clSyntaxError)
                {
                    Console.WriteLine(c_syntax);
                }
                else
                {
                    ProcessFiles(s_clFilePattern);
                }
            }
            catch (Exception err)
            {
                Console.WriteLine(err.ToString());
            }

            Win32Interop.ConsoleHelper.PromptAndWaitIfSoleConsole();
        }

        static void ParseCommandLine(string[] args)
        {
            if (args.Length != 1)
            {
                s_clSyntaxError = true;
                return;
            }

            s_clFilePattern = args[0];
        }

        static void ProcessFiles(string filePattern)
        {
            string directory = Path.GetDirectoryName(filePattern);
            directory = string.IsNullOrEmpty(directory)
                ? Environment.CurrentDirectory
                : Path.GetFullPath(directory);
            string pattern = Path.GetFileName(filePattern);
            foreach(string filename in Directory.GetFiles(directory, pattern))
            {
                var processor = new FileProcessor(filename);
                if (processor.OutputExists)
                {
                    Console.WriteLine($"Skipping: {filename}");
                }
                else
                {
                    processor.ProcessFile();
                }
            }
        }

    }

    class FileProcessor
    {
        const string c_sceneThreshold = "0.4";
        const string c_outputSuffix = " scenes.csv";

        string m_inputFilename;
        string m_outputFilename;
        TextWriter m_output;

        public FileProcessor(string filename)
        {
            m_inputFilename = filename;
            if (!File.Exists(m_inputFilename))
                throw new ApplicationException($"File does not exist: {m_inputFilename}");

            m_outputFilename =
                Path.Combine(Path.GetDirectoryName(m_inputFilename),
                Path.GetFileNameWithoutExtension(m_inputFilename) + c_outputSuffix);
        }

        public bool OutputExists
        {
            get
            {
                return File.Exists(m_outputFilename);
            }
        }

        public void ProcessFile()
        {
            Console.WriteLine($"Processing: {m_inputFilename}");
            Console.WriteLine($"Results to: {m_outputFilename}");

            using (m_output = new StreamWriter(m_outputFilename, false, new UTF8Encoding(false)))
            {
                m_output.WriteLine("seconds");  // CSV header - only one column

                string appName = "ffmpeg.exe";
                string arguments = $"-hide_banner -i \"{m_inputFilename}\" -filter:v \"select='gt(scene,{c_sceneThreshold})',showinfo\" -f null -";

                var psi = new ProcessStartInfo(appName, arguments);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = false;
                psi.RedirectStandardError = true;

                using (var process = new Process { StartInfo = psi })
                {
                    process.ErrorDataReceived += Process_ErrorDataReceived;
                    process.Start();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
            }
            m_output = null;
        }

        private void Process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Data)) return;
            Console.Write(e.Data);
            Console.Write('\r');

            int i = e.Data.IndexOf("pts_time:");
            if (i > 0)
            {
                i += 9;
                int anchor = i;
                while (i < e.Data.Length && (char.IsDigit(e.Data[i]) || e.Data[i] == '.')) ++i;
                string seconds = e.Data.Substring(anchor, i - anchor);
                m_output.WriteLine(seconds);
                m_output.Flush();

                Console.WriteLine();
                Console.WriteLine($"    ==>{seconds}<==");
                Console.WriteLine();
            }
        }

    }
}
