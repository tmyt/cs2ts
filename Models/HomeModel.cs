using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace cs2ts.Models
{
    public class HomeModel
    {
        [BindProperty]
        public string Source { get; set; }
        public string Enums { get; set; }
        public string TypeMap { get; set; }
        public string Generated { get; set; }
        public List<string> Warnings { get; set; }
    }
}
