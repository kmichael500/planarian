export interface PagedResult<T> {
  pageNumber: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  results: T[];
}
