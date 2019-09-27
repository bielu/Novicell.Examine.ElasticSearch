using System;
using System.Collections.Generic;
using System.Linq;
using Examine;
using Examine.LuceneEngine.Providers;
using Lucene.Net.Index;
using Lucene.Net.Store;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Examine;

namespace Novicell.Examine.ElasticSearch
{
    public class ElasticSearchUmbracoIndex : ElasticSearchBaseIndex, IUmbracoIndex
    {
        private const string SpecialFieldPrefix = "__";

        public const string IndexPathFieldName = SpecialFieldPrefix + "Path";
        public const string NodeKeyFieldName = SpecialFieldPrefix + "Key";
        public const string IconFieldName = SpecialFieldPrefix + "Icon";
        public const string PublishedFieldName = SpecialFieldPrefix + "Published";
        private readonly IProfilingLogger _logger;
        public bool EnableDefaultEventHandler { get; set; } = true;

        /// <summary>
        /// The prefix added to a field when it is duplicated in order to store the original raw value.
        /// </summary>
        public const string RawFieldPrefix = SpecialFieldPrefix + "Raw_";

        /// <summary>
        /// Create a new <see cref="UmbracoExamineIndex"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fieldDefinitions"></param>
        /// <param name="luceneDirectory"></param>
        /// <param name="defaultAnalyzer"></param>
        /// <param name="profilingLogger"></param>
        /// <param name="validator"></param>
        /// <param name="indexValueTypes"></param>
        protected ElasticSearchUmbracoIndex(string name,
            ElasticSearchConfig connectionConfiguration,
            IProfilingLogger profilingLogger,
            FieldDefinitionCollection fieldDefinitions = null,
            string analyzer = null,
            IValueSetValidator validator = null)
            : base(name, luceneDirectory, fieldDefinitions, defaultAnalyzer, validator, indexValueTypes)
        {
            ProfilingLogger = profilingLogger ?? throw new ArgumentNullException(nameof(profilingLogger));

            //try to set the value of `LuceneIndexFolder` for diagnostic reasons
            if (luceneDirectory is FSDirectory fsDir)
                LuceneIndexFolder = fsDir.Directory;

            _diagnostics = new UmbracoExamineIndexDiagnostics(this, ProfilingLogger);
        }

        private readonly bool _configBased = false;

        protected IProfilingLogger ProfilingLogger { get; }

        /// <summary>
        /// When set to true Umbraco will keep the index in sync with Umbraco data automatically
        /// </summary>
        public bool EnableDefaultEventHandler { get; set; } = true;

        public bool PublishedValuesOnly { get; protected set; } = false;

        /// <inheritdoc />
        public IEnumerable<string> GetFields()
        {
            //we know this is a LuceneSearcher
            var searcher = (LuceneSearcher) GetSearcher();
            return searcher.GetAllIndexedFields();
        }

        /// <summary>
        /// override to check if we can actually initialize.
        /// </summary>
        /// <remarks>
        /// This check is required since the base examine lib will try to rebuild on startup
        /// </remarks>
        protected override void PerformDeleteFromIndex(IEnumerable<string> itemIds, Action<IndexOperationEventArgs> onComplete)
        {
            if (CanInitialize())
            {
                using (new SafeCallContext())
                {
                    base.PerformDeleteFromIndex(itemIds, onComplete);
                }
            }
        }

        /// <summary>
        /// Returns true if the Umbraco application is in a state that we can initialize the examine indexes
        /// </summary>
        /// <returns></returns>
        protected bool CanInitialize()
        {
            // only affects indexers that are config file based, if an index was created via code then
            // this has no effect, it is assumed the index would not be created if it could not be initialized
            return _configBased == false || Current.RuntimeState.Level == RuntimeLevel.Run;
        }

        /// <summary>
        /// overridden for logging
        /// </summary>
        /// <param name="ex"></param>
        protected override void OnIndexingError(IndexingErrorEventArgs ex)
        {
            ProfilingLogger.Error(GetType(), ex.InnerException, ex.Message);
            base.OnIndexingError(ex);
        }

        /// <summary>
        /// This ensures that the special __Raw_ fields are indexed correctly
        /// </summary>
        /// <param name="docArgs"></param>
        protected override void OnDocumentWriting(DocumentWritingEventArgs docArgs)
        {
            var d = docArgs.Document;

            foreach (var f in docArgs.ValueSet.Values.Where(x => x.Key.StartsWith(RawFieldPrefix)).ToList())
            {
                if (f.Value.Count > 0)
                {
                    //remove the original value so we can store it the correct way
                    d.RemoveField(f.Key);

                    d.Add(new Field(
                        f.Key,
                        f.Value[0].ToString(),
                        Field.Store.YES,
                        Field.Index.NO, //don't index this field, we never want to search by it
                        Field.TermVector.NO));
                }
            }

            base.OnDocumentWriting(docArgs);
        }

        /// <summary>
        /// Overridden for logging.
        /// </summary>
        protected override void AddDocument(Document doc, ValueSet valueSet, IndexWriter writer)
        {
            ProfilingLogger.Debug(GetType(),
                "Write lucene doc id:{DocumentId}, category:{DocumentCategory}, type:{DocumentItemType}",
                valueSet.Id,
                valueSet.Category,
                valueSet.ItemType);

            base.AddDocument(doc, valueSet, writer);
        }

        protected override void OnTransformingIndexValues(IndexingItemEventArgs e)
        {
            base.OnTransformingIndexValues(e);

            //ensure special __Path field
            var path = e.ValueSet.GetValue("path");
            if (path != null)
            {
                e.ValueSet.Set(IndexPathFieldName, path);
            }

            //icon
            if (e.ValueSet.Values.TryGetValue("icon", out var icon) && e.ValueSet.Values.ContainsKey(IconFieldName) == false)
            {
                e.ValueSet.Values[IconFieldName] = icon;
            }
        }
        
        #region IIndexDiagnostics


        public int DocumentCount => _diagnostics.DocumentCount;
        public int FieldCount => _diagnostics.FieldCount;

        #endregion
    }
}