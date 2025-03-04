﻿using Microsoft.Extensions.Logging;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Path = VL.Lib.IO.Path;

namespace VL.Devices.Orbbec
{
    [ProcessNode(Name = "ConfigWriter")]
    public class SaveConfigToFile : IDisposable
    {
        private readonly ILogger logger;
        private readonly SerialDisposable serialDisposable = new();

        public SaveConfigToFile([Pin(Visibility = Model.PinVisibility.Hidden)] NodeContext nodeContext)
        {
            logger = nodeContext.GetLogger();
        }

        public void Update(VideoIn? input, Path filePath, bool write)
        {
            if (input is null) 
                return;

            if (write)
            {
                serialDisposable.Disposable = input.AcquisitionStarted.Take(1)
                    .Subscribe(a =>
                    {
                        try
                        {
                            //a.PropertyMap.Serialize(filePath.ToString());
                        }
                        catch (Exception e)
                        {
                            logger.LogError(e, $"Failed to serialize camera configuration.");
                        }
                    });
            }
        }

        public void Dispose()
        {
            serialDisposable.Dispose();
        }
    }
}
