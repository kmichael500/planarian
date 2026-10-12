import { CaveSearchParamsVm } from "../Models/CaveSearchParamsVm";
import { CaveSearchSortByConstants } from "../Models/CaveSearchVm";
import {
  QueryBuilder,
  QueryOperator,
} from "../../Search/Services/QueryBuilder";
import { normalizeCaveMainSearch } from "./CaveSearchQueryHelpers";

describe("normalizeCaveMainSearch", () => {
  it("converts the former main name search and defaults it to relevance", () => {
    const queryBuilder = new QueryBuilder<CaveSearchParamsVm>("", false)
      .filterBy("name", QueryOperator.Contains, "Bangor")
      .setSort(CaveSearchSortByConstants.LengthFeet)
      .setSortDescending(false);

    normalizeCaveMainSearch(queryBuilder, "");

    expect(queryBuilder.getFieldValue("name")).toBeUndefined();
    expect(queryBuilder.getFieldValue("search")).toBe("Bangor");
    expect(
      queryBuilder.getOperatorValue("search", QueryOperator.Contains)
    ).toBe(QueryOperator.FreeText);
    expect(queryBuilder.getSortBy()).toBe(CaveSearchSortByConstants.Relevance);
    expect(queryBuilder.getSortDescending()).toBe(true);
  });

  it("preserves an explicitly encoded sort while migrating a legacy search", () => {
    const queryBuilder = new QueryBuilder<CaveSearchParamsVm>("", false)
      .filterBy("name", QueryOperator.Contains, "Bangor")
      .setSort(CaveSearchSortByConstants.Name)
      .setSortDescending(false);

    normalizeCaveMainSearch(queryBuilder, "sortBy=Name");

    expect(queryBuilder.getFieldValue("name")).toBeUndefined();
    expect(queryBuilder.getFieldValue("search")).toBe("Bangor");
    expect(queryBuilder.getSortBy()).toBe(CaveSearchSortByConstants.Name);
    expect(queryBuilder.getSortDescending()).toBe(false);
  });

  it("defaults an existing unified search to relevance when no sort is encoded", () => {
    const queryBuilder = new QueryBuilder<CaveSearchParamsVm>("", false)
      .filterBy("search", QueryOperator.FreeText, "Bangor")
      .setSort(CaveSearchSortByConstants.LengthFeet);

    normalizeCaveMainSearch(queryBuilder, "");

    expect(queryBuilder.getSortBy()).toBe(CaveSearchSortByConstants.Relevance);
    expect(queryBuilder.getSortDescending()).toBe(true);
  });

  it("preserves an explicitly encoded sort for an existing unified search", () => {
    const queryBuilder = new QueryBuilder<CaveSearchParamsVm>("", false)
      .filterBy("search", QueryOperator.FreeText, "Bangor")
      .setSort(CaveSearchSortByConstants.LengthFeet)
      .setSortDescending(false);

    normalizeCaveMainSearch(queryBuilder, "sortBy=LengthFeet");

    expect(queryBuilder.getFieldValue("search")).toBe("Bangor");
    expect(queryBuilder.getSortBy()).toBe(CaveSearchSortByConstants.LengthFeet);
    expect(queryBuilder.getSortDescending()).toBe(false);
  });
});
