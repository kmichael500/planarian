import { fireEvent, render, screen } from "@testing-library/react";
import { AdvancedSearchDrawerComponent } from "./AdvancedSearchDrawerComponent";
import { QueryBuilder, QueryOperator } from "../Services/QueryBuilder";

interface SearchParams {
  search: string;
}

const renderSearch = (queryBuilder: QueryBuilder<SearchParams>) =>
  render(
    <AdvancedSearchDrawerComponent<SearchParams>
      queryBuilder={queryBuilder}
      onSearch={async () => undefined}
      mainSearchField="search"
      mainSearchFieldLabel="Search"
      mainSearchOperator={QueryOperator.FreeText}
      mainSearchSortBy="Relevance"
      mainSearchEmptySortBy="LengthFeet"
    />
  );

describe("AdvancedSearchDrawerComponent main search sorting", () => {
  it("uses relevance when starting a new search", () => {
    const queryBuilder = new QueryBuilder<SearchParams>("", false)
      .setSort("LengthFeet")
      .setSortDescending(false);

    renderSearch(queryBuilder);
    fireEvent.change(screen.getByPlaceholderText("Search"), {
      target: { value: "Bangor" },
    });

    expect(queryBuilder.getSortBy()).toBe("Relevance");
    expect(queryBuilder.getSortDescending()).toBe(true);
  });

  it("preserves an explicit sort while editing an existing search", () => {
    const queryBuilder = new QueryBuilder<SearchParams>("", false)
      .filterBy("search", QueryOperator.FreeText, "Bangor")
      .setSort("LengthFeet")
      .setSortDescending(false);

    renderSearch(queryBuilder);
    fireEvent.change(screen.getByPlaceholderText("Search"), {
      target: { value: "Bangor cave" },
    });

    expect(queryBuilder.getSortBy()).toBe("LengthFeet");
    expect(queryBuilder.getSortDescending()).toBe(false);
  });

  it("returns to the empty-search default only when relevance was active", () => {
    const queryBuilder = new QueryBuilder<SearchParams>("", false)
      .filterBy("search", QueryOperator.FreeText, "Bangor")
      .setSort("Relevance")
      .setSortDescending(true);

    renderSearch(queryBuilder);
    fireEvent.change(screen.getByPlaceholderText("Search"), {
      target: { value: "" },
    });

    expect(queryBuilder.getSortBy()).toBe("LengthFeet");
    expect(queryBuilder.getSortDescending()).toBe(true);
  });
});
