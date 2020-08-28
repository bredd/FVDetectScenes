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
@"Syntax: FVDetectScenes <filename> [-sceneThreshold <value>]
   Filename should be a video file such as .mp4.
   Wildcards are acceptable.

   Default scene threshold is 0.4. Should be a decimal number between
   0.0 and 1.0";

        static bool s_clSyntaxError;
        static string s_clFilePattern;
        static double s_clSceneThreshold = 0.4;

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
            for (int argi = 0; argi < args.Length; ++argi)
            {
                var arg = args[argi];

                switch (arg.ToLowerInvariant())
                {
                    case "-h":
                    case "-?":
                    case "-help":
                    case "/h":
                    case "/?":
                        s_clSyntaxError = true;
                        return;

                    case "-scenethreshold":
                        if (argi > args.Length - 2)
                        {
                            throw new Exception("No value for -sceneThreshold");
                        }
                        ++argi;
                        s_clSceneThreshold = double.Parse(args[argi]);
                        break;

                    default:
                        if (s_clFilePattern == null)
                        {
                            s_clFilePattern = arg;
                        }
                        else
                        {
                            throw new Exception("Unexpected argument: " + arg);
                        }
                        break;
                }
            }
            if (s_clFilePattern == null)
            {
                s_clSyntaxError = true;
            }
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
                var processor = new FileProcessor(filename, s_clSceneThreshold);
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
        const string c_outputSuffix = " scenes.csv";

        string m_inputFilename;
        string m_outputFilename;
        TextWriter m_output;

        string m_sceneThreshold = "0.4";

        public FileProcessor(string filename, double sceneThreshold)
        {
            m_inputFilename = filename;
            if (!File.Exists(m_inputFilename))
                throw new ApplicationException($"File does not exist: {m_inputFilename}");

            m_outputFilename =
                Path.Combine(Path.GetDirectoryName(m_inputFilename),
                Path.GetFileNameWithoutExtension(m_inputFilename) + c_outputSuffix);

            m_sceneThreshold = sceneThreshold.ToString();
            Console.WriteLine("SceneThreshold: " + m_sceneThreshold);
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
                string arguments = $"-hide_banner -i \"{m_inputFilename}\" -filter:v \"select='gt(scene,{m_sceneThreshold})',showinfo\" -f null -";

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
