using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Selen.Sites {
    public enum OfferStatus {
        Active,
        NotActive,
        Deleted,
        Draft
    }
    class Offer {
        public string Name { get; set; }
        public string Id { get; set; }
        public int Price { get; set; }
        public string ImageUrl { get; set; }
        public OfferStatus Status { get; set; }
    }
}