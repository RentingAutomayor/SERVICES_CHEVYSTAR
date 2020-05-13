using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiddlewareChevyStar.Models
{
    [Serializable()]
    public class ASSET
    {
        [System.Xml.Serialization.XmlElement("ASSETNUM")]
        public string ASSETNUM { get; set; }

        [System.Xml.Serialization.XmlElement("RAULTIMAMEDICION")]
        public double RAULTIMAMEDICION { get; set; }
    }
}
