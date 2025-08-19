using System.Collections.Generic;

namespace YAMIOS.Stuff
{
    public class Component
    {
        public required string Name { get; set; }
        public Dictionary<string, object> Fields { get; set; } = new(); // lol
    }
}