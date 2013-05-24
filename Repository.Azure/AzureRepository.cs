﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.Storage;
using Newtonsoft.Json;

namespace Repository.Azure
{
    public class AzureRepository<T> : Repository.Repository<T> where T : class
    {
        private static readonly String[] EmulatorConnectionStrings = new[]
        {
            "UseDevelopmentStorage=true",
            "DefaultEndpointsProtocol=https;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;"
        };

        //===============================================================
        public AzureRepository(Func<T, object> keySelector, String connectionString)
            : this(x => new[] { keySelector(x) }, connectionString)
        {}
        //===============================================================
        public AzureRepository(Func<T, object[]> keySelector, String connectionString)
            : this(keySelector, EmulatorConnectionStrings.Contains(connectionString) ? CloudStorageAccount.DevelopmentStorageAccount : CloudStorageAccount.Parse(connectionString))
        {
            ConnectionString = connectionString;
        }
        //===============================================================
        internal AzureRepository(Func<T, object[]> keySelector, CloudStorageAccount storageAccount)
            : base(keySelector)
        {
            ContainerName = AzureUtility.GetSanitizedContainerName<T>();
            AzureApi = new AzureApi(storageAccount);
            PendingChanges = new List<IPendingChange>();
        }
        //===============================================================
        public static AzureRepository<T> FromExplicitConnectionString(Func<T, object[]> keySelector, String connectionString)
        {
            return new AzureRepository<T>(keySelector, connectionString);
        }
        //===============================================================
        public static AzureRepository<T> FromExplicitConnectionString(Func<T, object> keySelector, String connectionString)
        {
            return new AzureRepository<T>(keySelector, connectionString);
        }
        //===============================================================
        public static AzureRepository<T> FromNamedConnectionString(Func<T, object[]> keySelector, String connectionStringName)
        {
            var connStr = CloudConfigurationManager.GetSetting(connectionStringName);
            return FromExplicitConnectionString(keySelector, connStr);
        }
        //===============================================================
        public static AzureRepository<T> FromNamedConnectionString(Func<T, object> keySelector, String connectionStringName)
        {
            var connStr = CloudConfigurationManager.GetSetting(connectionStringName);
            return FromExplicitConnectionString(keySelector, connStr);
        }
        //===============================================================
        public static AzureRepository<T> ForStorageEmulator(Func<T, object[]> keySelector)
        {
            return new AzureRepository<T>(keySelector, CloudStorageAccount.DevelopmentStorageAccount);
        }
        //===============================================================
        public static AzureRepository<T> ForStorageEmulator(Func<T, object> keySelector)
        {
            return ForStorageEmulator(x => new[] { keySelector(x) });
        }
        //===============================================================
        private String ContainerName { get; set; }
        //===============================================================
        private String ConnectionString { get; set; }
        //===============================================================
        private AzureApi AzureApi { get; set; }
        //===============================================================
        private IList<IPendingChange> PendingChanges { get; set; }
        //===============================================================
        public override void Insert(T value)
        {
            PendingChanges.Add(new AzureInsert<T>(KeySelector(value), value, AzureApi, ContainerName));
        }
        //===============================================================
        public override void RemoveByKey(params object[] keys)
        {
            PendingChanges.Add(new AzureRemove(keys, AzureApi, ContainerName));
        }
        //===============================================================
        public override void SaveChanges()
        {
            foreach (var change in PendingChanges)
                change.Apply();
        }
        //===============================================================
        public override bool ExistsByKey(params object[] keys)
        {
            return AzureApi.Exists(keys, ContainerName);
        }
        //===============================================================
        public override void Update<TValue>(TValue value, params object[] keys)
        {
            var existingObj = AzureApi.GetObject<T>(keys, ContainerName);
            PendingChanges.Add(new AzureModify<T>(existingObj, keys, x => AutoMapper.Mapper.DynamicMap(value, x), AzureApi, ContainerName));
        }
        //===============================================================
        public override void Update<TValue, TProperty>(TValue value, Func<T, TProperty> getter, params object[] keys)
        {
            var existingObj = AzureApi.GetObject<T>(keys, ContainerName);
            PendingChanges.Add(new AzureModify<T>(existingObj, keys, x => AutoMapper.Mapper.DynamicMap(value, getter(x)), AzureApi, ContainerName));
        }
        //===============================================================
        public override void Update(string json, UpdateType updateType, params object[] keys)
        {
            throw new NotImplementedException();
        }
        //===============================================================
        public override void Update(string pathToProperty, string json, UpdateType updateType, params object[] keys)
        {
            throw new NotImplementedException();
        }
        //===============================================================
        public override ObjectContext<T> Find(params object[] keys)
        {
            var obj = AzureApi.GetObject<T>(keys, ContainerName);
            return new ObjectContext<T>(obj);
        }
        //===============================================================
        public Uri GetObjectUri(params Object[] keys)
        {
            return AzureApi.GetObjectUri(keys, ContainerName);
        }
        //===============================================================
        public override EnumerableObjectContext<T> Items
        {
            get { return new EnumerableObjectContext<T>(AzureApi.EnumerateObjects<T>(ContainerName).AsQueryable()); }
        }
        //===============================================================
        public override void Dispose()
        {
            // Don't do anything here
        }
        //===============================================================
    }

    public class AzureRepository<TValue, TKey> : Repository<TValue, TKey> where TValue : class
    {
        //===============================================================
        public AzureRepository(Func<TValue, TKey> keySelector, String connectionString)
            : base(new AzureRepository<TValue>(x => keySelector(x), connectionString))
        {}
        //===============================================================
        internal AzureRepository(Func<TValue, TKey> keySelector, CloudStorageAccount storageAccount)
            : base(new AzureRepository<TValue>(x => new object[] { keySelector(x) }, storageAccount))
        {}
        //===============================================================
        public static AzureRepository<TValue, TKey> CreateForStorageEmulator(Func<TValue, TKey> keySelector)
        {
            return new AzureRepository<TValue, TKey>(keySelector, CloudStorageAccount.DevelopmentStorageAccount);
        }
        //===============================================================
        public static AzureRepository<TValue, TKey> FromExplicitConnectionString(Func<TValue, TKey> keySelector, String connectionString)
        {
            return new AzureRepository<TValue, TKey>(keySelector, connectionString);
        }
        //===============================================================
        public static AzureRepository<TValue, TKey> FromNamedConnectionString(Func<TValue, TKey> keySelector, String connectionStringName)
        {
            var connStr = CloudConfigurationManager.GetSetting(connectionStringName);
            return FromExplicitConnectionString(keySelector, connStr);
        }
        //===============================================================
        public Uri GetObjectUri(TKey key)
        {
            return (InnerRepository as AzureRepository<TValue>).GetObjectUri(key);
        }
        //===============================================================
    }

    public class AzureRepository<TValue, TKey1, TKey2> : Repository<TValue, TKey1, TKey2> where TValue : class
    {
        //===============================================================
        public AzureRepository(Func<TValue, Tuple<TKey1, TKey2>> keySelector, String connectionString)
            : base(new AzureRepository<TValue>(x => new object[] { keySelector(x).Item1, keySelector(x).Item2 }, connectionString))
        { }
        //===============================================================
        internal AzureRepository(Func<TValue, Tuple<TKey1, TKey2>> keySelector, CloudStorageAccount storageAccount)
            : base(new AzureRepository<TValue>(x => new object[] { keySelector(x).Item1, keySelector(x).Item2 }, storageAccount))
        { }
        //===============================================================
        public static AzureRepository<TValue, TKey1, TKey2> CreateForStorageEmulator(Func<TValue, Tuple<TKey1, TKey2>> keySelector)
        {
            return new AzureRepository<TValue, TKey1, TKey2>(keySelector, CloudStorageAccount.DevelopmentStorageAccount);
        }
        //===============================================================
        public static AzureRepository<TValue, TKey1, TKey2> FromExplicitConnectionString(Func<TValue, Tuple<TKey1, TKey2>> keySelector, String connectionString)
        {
            return new AzureRepository<TValue, TKey1, TKey2>(keySelector, connectionString);
        }
        //===============================================================
        public static AzureRepository<TValue, TKey1, TKey2> FromNamedConnectionString(Func<TValue, Tuple<TKey1, TKey2>> keySelector, String connectionStringName)
        {
            var connStr = CloudConfigurationManager.GetSetting(connectionStringName);
            return FromExplicitConnectionString(keySelector, connStr);
        }
        //===============================================================
        public Uri GetObjectUri(TKey1 key1, TKey2 key2)
        {
            return (InnerRepository as AzureRepository<TValue>).GetObjectUri(key1, key2);
        }
        //===============================================================
    }
}