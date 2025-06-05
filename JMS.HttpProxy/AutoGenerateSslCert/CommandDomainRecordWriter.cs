using Jack.Acme;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JMS.HttpProxy.AutoGenerateSslCert
{
    public class CommandDomainRecordWriter : IAcmeDomainRecoredWriter
    {
        private readonly string[] _command;

        public CommandDomainRecordWriter(string[] command)
        {
            _command = command;
        }
        public async Task WriteAsync(string domainName, string value)
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _command[0],
                Arguments = string.Join(" ", _command.Skip(1).Select(m=>$"\"{m}\"")),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var process = System.Diagnostics.Process.Start(startInfo);
            await process.WaitForExitAsync();
            if( process.ExitCode != 0 )
                throw new Exception($"Command '{startInfo.FileName} {startInfo.Arguments}' failed with exit code {process.ExitCode} for domain '{domainName}' with value '{value}'.");
        }
    }
}
