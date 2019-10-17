using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Examine;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence.DatabaseModelDefinitions;
using Umbraco.Core.Scoping;
using Umbraco.Core.Services;
using Umbraco.Core.Services.Changes;
using Umbraco.Core.Sync;
using Umbraco.Examine;
using Umbraco.Web.Cache;
using Umbraco.Web.Search;
using Novicell.Examine.ElasticSearch.ContentTypes;
using Novicell.Examine.ElasticSearch.Indexers;

namespace Novicell.Examine.ElasticSearch
{
    public class ElasticSearchExamineComponent : IComponent, Umbraco.Core.Composing.IComponent
    {
        private readonly IExamineManager _examineManager;
        private readonly IUmbracoIndexesCreator _indexCreator;
        private readonly IProfilingLogger _logger;

        public ElasticSearchExamineComponent(IExamineManager examineManager,
            IUmbracoIndexesCreator indexCreator,
            IProfilingLogger profilingLogger
        )
        {
            _examineManager = examineManager;
            _indexCreator = indexCreator;
            _logger = profilingLogger;

        }
        
        public void Initialize()
        {
            /*
            foreach (var index in _indexCreator.Create())
            {
                _examineManager.AddIndex(index);
                ElasticSearchBaseIndex luceneBaseIndex = (ElasticSearchBaseIndex) index;
            }
*/
            _logger.Debug<ElasticSearchExamineComponent>("Examine shutdown registered with MainDom");

            var registeredIndexers = _examineManager.Indexes.OfType<IIndex>().Count();

            _logger.Info<ExamineComponent>("Adding examine event handlers for {RegisteredIndexers} index providers.",
                registeredIndexers);

            // don't bind event handlers if we're not suppose to listen
            if (registeredIndexers == 0)
                return;

        }


        public void Terminate()
        {
        }

        public void Dispose()
        {
            Disposed?.Invoke(this, new System.EventArgs());
        }

    

        public ISite Site { get; set; }
        public event EventHandler Disposed;
    }
}