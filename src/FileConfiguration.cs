﻿using Path = VL.Lib.IO.Path;
using System.Collections.Immutable;
using VL.Devices.Orbbec.Advanced;

namespace VL.Devices.Orbbec
{
    /*[ProcessNode(Name = "ConfigReader")]
    public class FileConfigurationNode
    {
        IConfiguration? configuration;
        Path? file;

        [return: Pin(Name = "Output")]
        public IConfiguration Update(
            Path FilePath,
            bool Read)
        {
            if (Read)
            {
                this.file = FilePath;
                configuration = new FileConfiguration(FilePath);
            }
            return configuration!;
        }
    }
    
    class FileConfiguration : IConfiguration
    {
        public Path File { get; }
        public FileConfiguration(Path file)
        {
            File = file;
        }
        
        public void Configure(PropertyMap propertyMap)
        {
            if(File.Exists) propertyMap.DeSerialize(File.ToString());
        }
    }*/
}
