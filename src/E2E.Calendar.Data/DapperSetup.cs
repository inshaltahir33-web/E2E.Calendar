using Dapper;
using E2E.Calendar.Core.Models;
using E2E.Calendar.Core.Sync;
using E2E.Calendar.Data.Types;

namespace E2E.Calendar.Data;

public static class DapperSetup
{
    private static readonly object Gate = new();
    private static bool _configured;

    public static void Configure()
    {
        if (_configured)
        {
            return;
        }

        lock (Gate)
        {
            if (_configured)
            {
                return;
            }

            SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());
            SqlMapper.AddTypeHandler(new BooleanHandler());
            SqlMapper.AddTypeHandler(new IntEnumHandler<Priority>());
            SqlMapper.AddTypeHandler(new TextEnumHandler<AlertState>());
            SqlMapper.AddTypeHandler(new TextEnumHandler<OwnerType>());
            SqlMapper.AddTypeHandler(new TextEnumHandler<Provider>());
            SqlMapper.AddTypeHandler(new TextEnumHandler<OutboxOp>());

            _configured = true;
        }
    }
}
