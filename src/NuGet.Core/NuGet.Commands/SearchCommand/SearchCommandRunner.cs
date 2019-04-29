using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Commands
{
    public class SearchCommandRunner : ISearchCommandRunner
    {
        public async Task ExecuteCommand(SearchArgs listArgs)
        {
            await PrintPackages();
        }

        private Task PrintPackages()
        {
            Console.WriteLine("Hola");
            return null;
        }
    }
}
