--_NodeTableColumnCollection
alter table _NodeTableColumnCollection
add TableId bigint
go
update _NodeTableColumnCollection
set TableId = tid
from
(
select n.TableId as tid, n.TableSchema as ts, n.TableName as tn
from _NodeTableCollection as n
) as ntc
where ntc.ts = TableSchema and ntc.tn = TableName
go
alter table _NodeTableColumnCollection
alter column TableId bigint not null
go

-- _EdgeAttributeCollection
alter table _EdgeAttributeCollection
add ColumnId bigint
go
update _EdgeAttributeCollection
set ColumnId = cid
from
(
select n.ColumnId as cid, n.TableSchema as ts, n.TableName as tn, n.ColumnName as cn
from _NodeTableColumnCollection as n
) as ntc
where ntc.ts = TableSchema and ntc.tn = TableName and ntc.cn = ColumnName
go
alter table _EdgeAttributeCollection
alter column ColumnId bigint not null
go

-- _EdgeAverageDegreeCollection
alter table _EdgeAverageDegreeCollection
add SampleRowCount int default(1000), ColumnId bigint
go
update _EdgeAverageDegreeCollection
set ColumnId = cid
from
(
select n.ColumnId as cid, n.TableSchema as ts, n.TableName as tn, n.ColumnName as cn
from _NodeTableColumnCollection as n
) as ntc
where ntc.ts = TableSchema and ntc.tn = TableName and ntc.cn = ColumnName
go
alter table _EdgeAverageDegreeCollection
alter column ColumnId bigint not null
go



