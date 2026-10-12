import { QueryBuilder, QueryOperator } from "../../Search/Services/QueryBuilder";
import { CaveSearchParamsVm } from "../Models/CaveSearchParamsVm";
import { CaveSearchSortByConstants } from "../Models/CaveSearchVm";

export const normalizeCaveMainSearch = (
  queryBuilder: QueryBuilder<CaveSearchParamsVm>,
  queryString: string
) => {
  const legacyNameSearch = queryBuilder.getFieldValue("name") as
    | string
    | undefined;
  const hasUnifiedSearch = Boolean(
    (queryBuilder.getFieldValue("search") as string | undefined)?.trim()
  );
  const hasExplicitSort = new URLSearchParams(queryString).has("sortBy");

  if (!hasUnifiedSearch && legacyNameSearch?.trim()) {
    queryBuilder
      .removeFromDictionary("name")
      .filterBy("search", QueryOperator.FreeText, legacyNameSearch);
  }

  const hasSearch =
    hasUnifiedSearch || Boolean(legacyNameSearch?.trim());

  if (hasSearch && !hasExplicitSort) {
    queryBuilder
      .setSort(CaveSearchSortByConstants.Relevance)
      .setSortDescending(true);
  }

  return queryBuilder;
};
