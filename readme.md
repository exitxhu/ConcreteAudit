## Concorete Audit
 Automaticly track and audit all of your changes that are occured in a specified DbContext.
 this package is provider ignorant and only cares about the db context

### Usage

1- Decorate your entity with [Auditable()] attribute
2- your AppDbContext should inherite AuditDbContext and implement the constructor
3- in startup / program file you should inject it via AddAuditDbContext extension method, or you can inject it as scoped
4- you can use AuditDbContextOption for furture configuring CAudit behavior

**some configs are done inside _[Auditable()]_ attribute**

5- use contex.Audit<entity>(n=> n.predicate == true) to query the data
6- there are 3 properties in Audit<entity> class that help you see the data:
```
audited.CurrentDataData 
audited.OldData 
audited.Data : always carry the most relevent data, in insert and update it is CurrentData and if deleted it is OldData
```
### Audit patterns

you can have both Old and New data or only retaine new data.

to achive that you may use the optional parameter pattern in [Auditable()]


##### Updating:
if CAudit tracks a update change, the pre-modification data will be stored in OldData and post-modification inside CurrentData.

##### Inserting:
if CAudit tracks an insert, you can see it inside CurrentData.

##### Deleting:
if CAudit tracks a deleting opration, pre deleting data will be stored in OldData and CurrentData will be null-defualt.

### KeepNew
in this case CAudit dont care about the previuos state of entity

### KeepNewAndOld
if you set it as -KeepNewAndOld-, the audit record will behave like this in each scenario:

**Note: these options dont alter audit.Data behavior or content**
