using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

using System.Data;
using System.Data.SqlClient;

using Microsoft.SqlServer.Management.Smo;
using Microsoft.SqlServer.Management.Common;


namespace SQLUtils
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void buttonGenerate_Click(object sender, EventArgs e)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(string.Format("Server={0};Database={1};Trusted_Connection=True;", txtServerName.Text, txtDatabase.Text)))
                {
                    string sql;
                    if (txtSql.Text.Length > 0)
                        sql = txtSql.Text;
                    else
                        sql = "SELECT * FROM [" + txtSchema.Text + "].[" + txtTableName.Text + "]";

                    SqlCommand command = new SqlCommand(sql, conn);
                    SqlDataAdapter adapter = new SqlDataAdapter(command);
                    DataSet data = new DataSet();
                    adapter.Fill(data);
                    DataTable tableToScript = data.Tables[0];

                    SqlCommand identityColumnCommand = new SqlCommand(string.Format(@"SELECT DISTINCT COLUMN_NAME
                        from INFORMATION_SCHEMA.COLUMNS 
                        where TABLE_SCHEMA = '{0}' 
                        and TABLE_NAME = '{1}'
                        and COLUMNPROPERTY(object_id(TABLE_NAME), COLUMN_NAME, 'IsIdentity') = 1", txtSchema.Text, txtTableName.Text), conn
                        );

                    string identityColumnName = "";
                    try
                    {
                        conn.Open();
                        identityColumnName = (string)identityColumnCommand.ExecuteScalar();
                    }
                    finally
                    {
                        conn.Close();
                    }

                    string insertIntoPart = "INSERT INTO [" + txtSchema.Text + "].[" + txtTableName.Text + "] (";

                    for (int columnIndex = 0; columnIndex < tableToScript.Columns.Count; columnIndex++)
                    {
                        string columnName = (tableToScript.Columns[columnIndex].ColumnName.StartsWith("["))?
                            tableToScript.Columns[columnIndex].ColumnName:
                                "[" + tableToScript.Columns[columnIndex].ColumnName + "]";

                        insertIntoPart += " " + columnName;
                        
                        if (columnIndex != tableToScript.Columns.Count - 1)
                            insertIntoPart += ",";

                    }
                    insertIntoPart += ") VALUES (";

                    StringBuilder script = new StringBuilder();

                    if (identityColumnName != null)
                        script.Append("SET IDENTITY_INSERT [" + txtSchema.Text + "].[" + txtTableName.Text + "] ON\n\n");

                    for (int rowIndex = 0; rowIndex < tableToScript.Rows.Count; rowIndex++)
                    {
                        script.Append(insertIntoPart);

                        DataRow rowToScript = tableToScript.Rows[rowIndex];
                        for (int columnIndex = 0; columnIndex < tableToScript.Columns.Count; columnIndex++)
                        {
                            if (System.Convert.IsDBNull(rowToScript[columnIndex]))
                            {
                                script.Append(" NULL");
                            }
                            else
                                if (tableToScript.Columns[columnIndex].DataType == typeof(DateTime))
                                {
                                    script.Append(" '" + ((DateTime)rowToScript[columnIndex]).ToString("yyyy-MM-ddTHH:mm:ss.fff") + "'");
                                }
                                else if (tableToScript.Columns[columnIndex].DataType == typeof(bool))
                                {
                                    script.Append(" " + ((bool)rowToScript[columnIndex] ? 1 : 0).ToString());
                                }
                                else
                                {
                                    //for string or other types
                                    script.Append(" '" + rowToScript[columnIndex].ToString().Replace("'", "''") + "'");
                                }
                                    

                            if (columnIndex != tableToScript.Columns.Count - 1)
                                script.Append(",");
                        }

                        script.Append(")\n");
                    }

                    if (identityColumnName != null)
                        script.Append("\n SET IDENTITY_INSERT [" + txtSchema.Text + "].[" + txtTableName.Text + "] OFF \n");


                    txtScript.Text = script.ToString();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        private void buttonCheckSPs_Click(object sender, EventArgs e)
        {
            WinProgressDialog.ProgressDialog progress = new WinProgressDialog.ProgressDialog();
            progress.Show(this.Handle.ToInt32(), "Checking Procs", "Initializing...");
            try
            {
                Microsoft.SqlServer.Management.Smo.Server sourceServer = new Server(txtServerName.Text);
                Microsoft.SqlServer.Management.Smo.Database sourceDB = sourceServer.Databases[txtDatabase.Text];

                Microsoft.SqlServer.Management.Smo.ScriptingOptions so = new ScriptingOptions();
                so.ExtendedProperties = false;
                so.IncludeIfNotExists = true;

                progress.MaxValue = sourceDB.StoredProcedures.Count;

                for (int i = 0; i < sourceDB.StoredProcedures.Count; i++)
                {
                    if ((!sourceDB.StoredProcedures[i].IsSystemObject) && (!sourceDB.StoredProcedures[i].IsEncrypted))
                    {
                        if (progress.UpdateProgress(i, "Checking (" + i.ToString() + @" of " + progress.MaxValue.ToString() + ") " + sourceDB.StoredProcedures[i] + " ..."))
                            break;
                        try
                        {
                            sourceDB.StoredProcedures[i].TextMode = false;
                            sourceDB.StoredProcedures[i].Recompile = true;
                            sourceDB.StoredProcedures[i].Alter();
                        }
                        catch (Exception ex)
                        {
                            txtSPRecreateResults.Text += sourceDB.StoredProcedures[i].Name + " failed: " + GetInnerMostExceptionMessage(ex) + "\n\n";
                        }
                    }
                }

                txtSPRecreateResults.Text += "All checks completed.";
            }
            finally
            {
                progress.Dispose();
            }
        }

        private string GetInnerMostExceptionMessage(Exception ex)
        {
            Exception inner = ex;
            while (inner.InnerException != null)
                inner = inner.InnerException;
            return inner.Message;
        }
    }
}