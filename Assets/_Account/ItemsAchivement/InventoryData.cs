using System;
using System.Collections.Generic;

namespace DreamClass.ItemsAchivement
{
    [System.Serializable]
    public class InventoryResponse
    {
        public string message;
        public int count;
        public List<InventoryItem> data;
    }

    [System.Serializable]
    public class InventoryItem
    {
        public string itemId;
        public int quantity;
        public string obtainedDate;
        public bool isEquipped;
        public string notes;
        public string _id;
        public ItemDetails itemDetails;
    }

    [System.Serializable]
    public class ItemDetails
    {
        public string _id;
        public string itemId;
        public string name;
        public string image; // URL
        public string type;
        public string description;
        public string notes;
    }
}
