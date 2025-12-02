using System;
using System.Collections.Generic;

namespace DreamClass.SpinWheel
{
    [Serializable]
    public class SpinWheelResponse
    {
        public string message;
        public List<SpinWheelData> data;
    }

    [Serializable]
    public class SpinWheelData
    {
        public string _id;
        public string name;
        public string description;
        public int spinPrice;
        public string currency;
        public string startTime;
        public string endTime;
        public bool isActive;
        public List<SpinWheelItem> items;
        public string createdAt;
        public string updatedAt;
    }

    [Serializable]
    public class SpinWheelItem
    {
        public string itemId;
        public float rate;
        public string _id;
        public ItemDetails itemDetails;
    }

    [Serializable]
    public class ItemDetails
    {
        public string _id;
        public string itemId;
        public string name;
        public string image;
        public string type;
        public string description;
        public string notes;
        public string createdAt;
        public string updatedAt;
    }
}
