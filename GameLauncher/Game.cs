using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameLauncher
{
    internal class Game
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public string LocalImagePath { get; set; }
        public DateTime ReleaseDate { get; set; }

        public Game(int id, string name, string description, string imageUrl, string localImagePath, DateTime releaseDate)
        {
            Id = id;
            Name = name;
            Description = description;
            ImageUrl = imageUrl;
            LocalImagePath = localImagePath;
            ReleaseDate = releaseDate;
        }
    }
}
