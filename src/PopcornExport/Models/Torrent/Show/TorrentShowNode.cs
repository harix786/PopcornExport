﻿using MongoDB.Bson.Serialization.Attributes;
using System.Runtime.Serialization;

namespace PopcornExport.Models.Torrent.Show
{
    [BsonIgnoreExtraElements]
    [DataContract]
    public class TorrentShowNode
    {
        [DataMember]
        [BsonElement("0")]
        public TorrentShow Torrent_0 { get; set; }

        [DataMember]
        [BsonElement("480p")]
        public TorrentShow Torrent_480p { get; set; }

        [DataMember]
        [BsonElement("720p")]
        public TorrentShow Torrent_720p { get; set; }

        [DataMember]
        [BsonElement("1080p")]
        public TorrentShow Torrent_1080p { get; set; }
    }

}