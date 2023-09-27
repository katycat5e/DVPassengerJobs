﻿using DV.Localization;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PassengerJobs
{
    public enum LocalizationKey
    {
        // License
        LICENSE_NAME,
        LICENSE_DESCRIPTION,

        LICENSE_ITEM_NAME,
        LICENSE_SAMPLE_ITEM_NAME,

        // Cargo
        CARGO_NAME_FULL,
        CARGO_NAME_SHORT,

        // Jobs
        JOB_EXPRESS_NAME,
        JOB_EXPRESS_DESCRIPTION,
        JOB_EXPRESS_DIRECT_DESC,
        JOB_EXPRESS_COVER,

        // Signs
        SIGN_INCOMING_TRAIN,
        SIGN_OUTGOING_TRAIN,
        SIGN_EXPRESS_NAME,
        SIGN_BOARDING,
        SIGN_DEPARTING,
    }

    public static class LocalizationKeyExtensions
    {
        private static readonly Dictionary<LocalizationKey, string> _keyValues;

        static LocalizationKeyExtensions()
        {
            _keyValues = Enum.GetValues(typeof(LocalizationKey))
                .Cast<LocalizationKey>()
                .ToDictionary(
                    k => k,
                    k => $"passjobs/{Enum.GetName(typeof(LocalizationKey), k).ToLower()}"
                );
        }

        /// <summary>Get localization key string</summary>
        public static string K(this LocalizationKey key) => _keyValues[key];

        /// <summary>Get localized string</summary>
        public static string L(this LocalizationKey key, params string[] values) => LocalizationAPI.L(K(key), values);
    }
}