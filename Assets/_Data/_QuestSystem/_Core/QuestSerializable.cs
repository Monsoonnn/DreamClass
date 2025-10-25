using System.Collections.Generic;
using UnityEngine;

namespace DreamClass.QuestSystem
{
    public enum QuestState
    {
        NOT_PREMISE,  // Không đủ điều kiện khởi động (chưa unlock)
        NOT_START,    // Có thể bắt đầu, nhưng chưa start
        IN_PROGRESS,  // Đang làm
        FINISHED      // Đã hoàn thành
    }
}
