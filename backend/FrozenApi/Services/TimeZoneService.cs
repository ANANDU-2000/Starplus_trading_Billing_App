/*
Purpose: Timezone service for Gulf Standard Time (GST, UTC+4) - Abu Dhabi/UAE
Author: AI Assistant
Date: 2025
*/
using System;

namespace FrozenApi.Services
{
    public interface ITimeZoneService
    {
        DateTime GetCurrentTime();
        DateTime GetCurrentDate();
        DateTime ConvertToGst(DateTime utcDateTime);
        DateTime ConvertToUtc(DateTime gstDateTime);
        TimeZoneInfo GetGstTimeZone();
    }

    public class TimeZoneService : ITimeZoneService
    {
        // Gulf Standard Time (Abu Dhabi, UAE) - UTC+4
        private static readonly TimeZoneInfo _gstTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Arabian Standard Time");

        /// <summary>
        /// Gets the current time in Gulf Standard Time (GST, UTC+4)
        /// </summary>
        public DateTime GetCurrentTime()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _gstTimeZone);
        }

        /// <summary>
        /// Gets the current date (no time) in Gulf Standard Time
        /// </summary>
        public DateTime GetCurrentDate()
        {
            return GetCurrentTime().Date;
        }

        /// <summary>
        /// Converts UTC datetime to Gulf Standard Time
        /// </summary>
        public DateTime ConvertToGst(DateTime utcDateTime)
        {
            if (utcDateTime.Kind != DateTimeKind.Utc)
            {
                utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
            }
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, _gstTimeZone);
        }

        /// <summary>
        /// Converts Gulf Standard Time to UTC for database storage
        /// </summary>
        public DateTime ConvertToUtc(DateTime gstDateTime)
        {
            return TimeZoneInfo.ConvertTimeToUtc(gstDateTime, _gstTimeZone);
        }

        /// <summary>
        /// Gets the GST timezone info
        /// </summary>
        public TimeZoneInfo GetGstTimeZone()
        {
            return _gstTimeZone;
        }
    }
}
