﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Athame.PluginAPI.Downloader;

namespace Athame.DownloadAndTag
{
    internal class AlbumArtFile : DownloadableFile
    {
        public byte[] Data { get; set; }

    }
}

