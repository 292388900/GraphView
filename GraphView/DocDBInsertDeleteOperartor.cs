﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GraphView
{

    internal class InsertEdgeOperator : GraphViewOperator
    {
        public GraphViewOperator SelectInput;
        public string edge;
        public string source, sink;
        public GraphViewConnection dbConnection;
        private bool UploadFinish;
        internal Dictionary<string, string> map;
        public string collection;


        public InsertEdgeOperator(GraphViewConnection dbConnection, string collection, GraphViewOperator SelectInput, string edge, string source, string sink)
        {
            this.dbConnection = dbConnection;
            this.SelectInput = SelectInput;
            this.edge = edge;
            this.source = source;
            this.sink = sink;
            this.collection = collection;
            Open();
        }

        #region Unfinish GroupBy-method
        /*public override Record Next()
        {
            Dictionary<string, List<string>> groupBySource = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
            Dictionary<string, List<string>> groupBySink = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();

            SelectInput.Open();
            while (SelectInput.Status())
            {
                Record rec = (Record)SelectInput.Next();
                string source = rec.RetriveData(null, 0);
                string sink = rec.RetriveData(null, 1);

                if (!groupBySource.ContainsKey(source))
                {
                    groupBySource[source] = new System.Collections.Generic.List<string>();
                }
                groupBySource[source].Add(sink);

                if (!groupBySink.ContainsKey(sink))
                {
                    groupBySink[sink] = new System.Collections.Generic.List<string>();
                }
                groupBySink[sink].Add(source);
            }
            SelectInput.Close();

            foreach (string source in groupBySource.Keys)
            {
                // Insert edges into the source doc
            }

            foreach (string sink in groupBySink.Keys)
            {
                // Insert reverse edges into the sink doc
            }

            return null;
        }*/
        #endregion

        public override Record Next()
        {
            if (!Status()) return null;
            map = new Dictionary<string, string>();

            while (SelectInput.Status())
            {
                //get source and sink's id from SelectQueryBlock's TraversalProcessor 
                Record rec = SelectInput.Next();
                if (rec == null) break;
                List<string> header = SelectInput.header;
                string sourceid = rec.RetriveData(header, source);
                string sinkid = rec.RetriveData(header, sink);
                string source_tital = source[0] + ".doc";
                string sink_tital = sink[0] + ".doc";

                string source_json_str = rec.RetriveData(header, source_tital);
                string sink_json_str = rec.RetriveData(header, sink_tital);
                
                InsertEdgeInMap(sourceid, sinkid,source_json_str, sink_json_str);
            }

            Upload();

            Close();
            return null;
        }

        internal void InsertEdgeInMap(string sourceid, string sinkid, string source_doc, string sink_doc)
        {
            if (!map.ContainsKey(sourceid))
                map[sourceid] = source_doc;
            
            if (!map.ContainsKey(sinkid))
                map[sinkid] = sink_doc;
            
            GraphViewDocDBCommand.INSERT_EDGE(map, edge, sourceid, sinkid);
        }

        internal void Upload()
        {
            UploadFinish = false;
            ReplaceDocument();

            //Wait until finish replacing.
            while (!UploadFinish)
                System.Threading.Thread.Sleep(10);
        }

        public async Task ReplaceDocument()
        {
            foreach (var cnt in map)
                await GraphViewDocDBCommand.ReplaceDocument(dbConnection, cnt.Key, cnt.Value);
            UploadFinish = true;
        }
    }

    internal class DeleteEdgeOperator : GraphViewOperator
    {
        public GraphViewOperator SelectInput;
        public string source, sink;
        public GraphViewConnection dbConnection;
        private bool UploadFinish;
        public string EdgeID_str;
        public string EdgeReverseID_str;
        internal Dictionary<string, string> map;
        public string collection;

        public DeleteEdgeOperator(GraphViewConnection dbConnection, string collection, GraphViewOperator SelectInput,  string source, string sink, string EdgeID_str, string EdgeReverseID_str)
        {
            this.dbConnection = dbConnection;
            this.SelectInput = SelectInput;
            this.source = source;
            this.sink = sink;
            this.EdgeID_str = EdgeID_str;
            this.EdgeReverseID_str = EdgeReverseID_str;
            this.collection = collection;
            Open();
        }

        public override Record Next()
        {
            if (!Status()) return null;
            map = new Dictionary<string, string>();

            while (SelectInput.Status())
            {
                //get source and sink's id from SelectQueryBlock's TraversalProcessor 
                Record rec = SelectInput.Next();
                if (rec == null) break;
                List<string> header = SelectInput.header;
                string sourceid = rec.RetriveData(header, source);
                string sinkid = rec.RetriveData(header, sink);
                //The "e" in the Record is "Reverse_e" in fact
                string EdgeReverseID = rec.RetriveData(header, EdgeID_str);
                string EdgeID = rec.RetriveData(header, EdgeReverseID_str);
                int ID, reverse_ID;
                int.TryParse(EdgeID, out ID);
                int.TryParse(EdgeReverseID, out reverse_ID);

                DeleteEdgeInMap(sourceid,sinkid,ID,reverse_ID);
            }

            Upload();

            Close();

            return null;
        }

        internal void DeleteEdgeInMap(string sourceid, string sinkid, int ID, int reverse_ID)
        {

            //Create one if a document not exist locally.
            if (!map.ContainsKey(sourceid))
            {
                var documents =
                    dbConnection.DocDBclient.CreateDocumentQuery(
                        "dbs/" + dbConnection.DocDB_DatabaseId + "/colls/" +
                        dbConnection.DocDB_CollectionId,
                        "SELECT * " +
                        string.Format("FROM doc WHERE doc.id = \"{0}\"", sourceid));
                foreach (var doc in documents)
                    map[sourceid] = JsonConvert.SerializeObject(doc);
            }
            if (!map.ContainsKey(sinkid))
            {
                var documents =
                    dbConnection.DocDBclient.CreateDocumentQuery(
                        "dbs/" + dbConnection.DocDB_DatabaseId + "/colls/" +
                        dbConnection.DocDB_CollectionId,
                        "SELECT * " +
                        string.Format("FROM doc WHERE doc.id = \"{0}\"", sinkid));
                foreach (var doc in documents)
                    map[sinkid] = JsonConvert.SerializeObject(doc);
            }

            map[sourceid] = GraphViewJsonCommand.Delete_edge(map[sourceid], ID);
            map[sinkid] = GraphViewJsonCommand.Delete_reverse_edge(map[sinkid], reverse_ID);
        }

        internal void Upload()
        {
            UploadFinish = false;
            ReplaceDocument();
            //wait until finish replacing.
            while (!UploadFinish)
                System.Threading.Thread.Sleep(10);
        }

        public async Task ReplaceDocument()
        {
            foreach (var cnt in map)
                await GraphViewDocDBCommand.ReplaceDocument(dbConnection, cnt.Key, cnt.Value);
            UploadFinish = true;
        }
    }

    internal class InsertNodeOperator : GraphViewOperator
    {
        public string Json_str;
        public GraphViewConnection dbConnection;
        public string collection;
        public bool UploadFinish;

        public InsertNodeOperator(GraphViewConnection dbConnection, string collection, string Json_str)
        {
            this.dbConnection = dbConnection;
            this.Json_str = Json_str;
            this.collection = collection;
            Open();
        }
        public override Record Next()
        {
            if (!Status()) return null;

            var obj = JObject.Parse(Json_str);

            Upload(obj);

            Close();
            return null;
        }

        void Upload(JObject obj)
        {
            UploadFinish = false;
            CreateDocument(obj);

            //Wait until finish Creating documents.
            while (!UploadFinish)
                System.Threading.Thread.Sleep(10);
        }

        public async Task CreateDocument(JObject obj)
        {
            await dbConnection.DocDBclient.CreateDocumentAsync("dbs/" + dbConnection.DocDB_DatabaseId + "/colls/" + dbConnection.DocDB_CollectionId, obj);
            UploadFinish = true;
        }
    }
    internal class DeleteNodeOperator : GraphViewOperator
    {
        public WBooleanExpression search;
        public string Selectstr;
        public GraphViewConnection dbConnection;
        public string collection;
        public bool UploadFinish;

        public DeleteNodeOperator(GraphViewConnection dbConnection,string collection,WBooleanExpression search, string Selectstr)
        {
            this.dbConnection = dbConnection;
            this.search = search;
            this.Selectstr = Selectstr;
            this.collection = collection;
            Open();
        }

        /// <summary>
        /// Check if there are some edges still connect to these nodes.
        /// </summary>
        /// <returns></returns>
        internal bool CheckNodes()
        {
            var sum_DeleteNode = dbConnection.DocDBclient.CreateDocumentQuery(
                                "dbs/" + dbConnection.DocDB_DatabaseId + "/colls/" + dbConnection.DocDB_CollectionId,
                                Selectstr);
            foreach (var DeleteNode in sum_DeleteNode)
                return false;
            return true;
        }

        /// <summary>
        /// Get those nodes.
        /// And then delete it.
        /// </summary>
        internal void DeleteNodes()
        {
            Selectstr = "SELECT * " + "FROM Node ";
            if (search != null)
                Selectstr += @"WHERE " + search.ToString();
            var sum_DeleteNode = dbConnection.DocDBclient.CreateDocumentQuery(
                "dbs/" + dbConnection.DocDB_DatabaseId + "/colls/" + dbConnection.DocDB_CollectionId,
                Selectstr);

            foreach (var DeleteNode in sum_DeleteNode)
            {
                UploadFinish = false;
                var docLink = string.Format("dbs/{0}/colls/{1}/docs/{2}", dbConnection.DocDB_DatabaseId,
                    dbConnection.DocDB_CollectionId, DeleteNode.id);
                DeleteDocument(docLink);
                //wait until finish deleting
                while (!UploadFinish)
                    System.Threading.Thread.Sleep(10);
            }
        }

        /// <summary>
        /// First check if there are some edges still connect to these nodes.
        /// If not, delete them.
        /// </summary>
        /// <returns></returns>
        public override Record Next()
        {
            if (!Status())
                return null;
            
            if (CheckNodes())
            {
                DeleteNodes();
            }
            else
            {
                Close();
                throw new GraphViewException("There are some edges still connect to these nodes.");
            }
            Close();
            return null;
        }

        public async Task DeleteDocument(string docLink)
        {
            await dbConnection.DocDBclient.DeleteDocumentAsync(docLink);
            UploadFinish = true;
        }
    }

}
