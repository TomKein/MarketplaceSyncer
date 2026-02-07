Class365 API: GET Requests Specification
1. General Structure
Endpoint: https://api.class365.ru/api/open/v1/{model_name} Format: JSON. Auth: Header Authorization: Bearer <token> or app_psw in params.

2. Filtering (filters)
Used for querying specific records.

Format: filters[field_name]=value (simple equality).

Operators:

filters[field_name][>=]=value — Greater than or equal.

filters[field_name][<=]=value — Less than or equal.

filters[field_name][>]=value — Greater than.

filters[field_name][<]=value — Less than.

filters[field_name][!=]=value — Not equal.

filters[field_name][in]=val1,val2 — Inclusion in list.

filters[field_name][like]=%text% — SQL-like search.

Logical OR: filters[OR][0][field1]=val1&filters[OR][0][field2]=val2

3. Pagination & Sorting
limit: Count of records (default 100, max 1000).

offset: Skip records for pagination.

sort: Field name. Use - for descending: sort=-id.

4. Expansion & Relations (with)
Used to fetch data from linked models instead of just IDs.

Syntax: with[relation_name]

Deep Expansion: with[relation1][with][relation2]

Filtering Relations: with[relation][filters][field]=value

Common Relations: organization, warehouse, counterparty, employee.

5. Custom Fields (add_fields)
User-defined properties of an entity.

Show all: with_add_fields=1

Filter by custom field: filters[add_field_ID]=value (where ID is numeric).

6. Output Control
fields: Comma-separated list of required fields to reduce payload.

request_count: If 1, returns total_count of all records matching filters.

7. Logic Examples for LLM
Search by Date Range: filters[date][>=]=2023-01-01&filters[date][<=]=2023-12-31

Fetch Orders with Items: model: customer_orders, params: with[items]

Filter by Archive: filters[archive]=0 (active only).

Note: This doc covers standard GET behavior for all models in Class365.