using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Configuration;
using System.Data;
using System.Data.Common;
using System.Text;

namespace Comaker.ORM
{
    public class TrackedOracleCommand : DbCommand, IDisposable
    {
        private readonly bool _enableParameterLogging;
        private OracleCommand _command;

        public TrackedOracleCommand(OracleCommand command)
        {
            _command = command;
            bool.TryParse(ConfigurationManager.AppSettings["EnableParameterLogging"], out _enableParameterLogging); ;
        }

        private static TelemetryClient _telemetryClient = new TelemetryClient(TelemetryConfiguration.Active);

        public int ArrayBindCount
        {
            get => _command.ArrayBindCount;
            set => _command.ArrayBindCount = value;
        }

        public override string CommandText
        {
            get => _command.CommandText;
            set => _command.CommandText = value;
        }

        public override int CommandTimeout
        {
            get => _command.CommandTimeout;
            set => _command.CommandTimeout = value;
        }

        public override CommandType CommandType
        {
            get => _command.CommandType;
            set => _command.CommandType = value;
        }

        public override bool DesignTimeVisible
        {
            get => _command.DesignTimeVisible;
            set => _command.DesignTimeVisible = value;
        }

        public override UpdateRowSource UpdatedRowSource
        {
            get => _command.UpdatedRowSource;
            set => _command.UpdatedRowSource = value;
        }

        protected override DbConnection DbConnection
        {
            get => _command.Connection;
            set => _command.Connection = value as OracleConnection;
        }

        protected override DbTransaction DbTransaction
        {
            get => _command.Transaction;
            set => _command.Transaction = value as OracleTransaction;
        }

        public bool BindByName
        {
            get => _command.BindByName;
            set => _command.BindByName = value;
        }

        protected override DbParameterCollection DbParameterCollection => _command.Parameters;

        protected override DbParameter CreateDbParameter() => _command.CreateParameter();

        public override void Prepare() => _command.Prepare();

        public override void Cancel() => _command.Cancel();

        public override int ExecuteNonQuery()
        {
            return TrackDependency("NonQuery", () => _command.ExecuteNonQuery());
        }

        public override object ExecuteScalar()
        {
            return TrackDependency("Scalar", () => _command.ExecuteScalar());
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            return TrackDependency("Reader", () => _command.ExecuteReader(behavior));
        }

        private TResult TrackDependency<TResult>(string type, Func<TResult> func)
        {
            if (_telemetryClient == null || !_telemetryClient.IsEnabled())
            {
                return func();
            }

            TResult result;
            using (var operation = _telemetryClient.StartOperation<DependencyTelemetry>(_command.GetType().FullName))
            {
                operation.Telemetry.Type = type;
                try
                {
                    result = func();
                    operation.Telemetry.Success = true;
                }
                finally
                {
                    var executedQuery = new StringBuilder();
                    executedQuery.Append(_command.CommandText);
                    if (_enableParameterLogging)
                    {
                        executedQuery.AppendLine();
                        executedQuery.AppendLine();
                        foreach (DbParameter parameter in _command.Parameters)
                        {
                            executedQuery.AppendLine($"{parameter.ParameterName} = {parameter.Value}");
                        }
                    }

                    operation.Telemetry.Data = executedQuery.ToString();
                }
            }

            return result;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _command != null) _command.Dispose();
            _command = null;

            base.Dispose(disposing);
        }
    }
}