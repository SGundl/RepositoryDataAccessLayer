using RepositoryDataAccessLayer.Helper;
using System.Collections.Generic;

namespace Polaris.RepositoryDataAccessLayer
{
    public interface IRepository
    {
        #region Public properties
        int MaxItemCount { get; set; }

        int MaxRetries { get; set; }

        string SystemUser { get; set; }

        string ApplicationName { get; set; }

        string ServerName { get; set; }

        string SplunkTarget { get; set; }

        string SplunkToken { get; set; }

        bool LogIfInfo { get; set; }
        #endregion

        #region Public methods
        T Insert<T>(string dataSource, T dataItem);
        T Update<T>(string dataSource, T updateItem, string id);
        T GetDataById<T>(string dataSource, string id);
        List<T> GetDataByKeys<T>(string dataSource, List<Filter> searchItems);
        List<T> GetAllData<T>(string dataSource);
        #endregion
    }
}
