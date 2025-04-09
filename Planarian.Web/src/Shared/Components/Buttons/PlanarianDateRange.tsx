// File: PlanarianDateRange.tsx

import React from "react";
import { DatePicker, Grid } from "antd";
import type { RangePickerProps } from "antd/lib/date-picker";
import dayjs, { Dayjs } from "dayjs";

const { RangePicker } = DatePicker;

export interface PlanarianDateRangeProps extends RangePickerProps {}

/**
 * A responsive date range picker component.
 *
 * - On medium screens (≥768px) and larger, it renders the standard RangePicker.
 * - On small screens (sm or smaller), it renders two separate DatePicker controls with date restrictions.
 *
 * For small screens:
 * - The start DatePicker disables dates after the selected end date.
 * - The end DatePicker disables dates before the selected start date.
 * - If either date is cleared, both fields are cleared.
 *
 * @param {PlanarianDateRangeProps} props - Props forwarded to the picker controls.
 */
const PlanarianDateRange: React.FC<PlanarianDateRangeProps> = (props) => {
  const { value = [null, null], onChange, ...restProps } = props;
  const screens = Grid.useBreakpoint();

  // If the screen is medium or larger, use the standard RangePicker.
  if (screens.md) {
    return <RangePicker value={value} onChange={onChange} {...restProps} />;
  } else {
    // For small screens, render two separate DatePicker controls.
    const [start, end] = value || [null, null];

    // Helper function to format a date to a string.
    const formatDateString = (date: Dayjs | null): string =>
      date ? date.format("YYYY-MM-DD") : "";

    return (
      <div style={{ display: "flex", gap: "8px" }}>
        <DatePicker
          value={start}
          placeholder="Start Date"
          // Disable any date that is after the currently selected end date.
          disabledDate={(current: Dayjs) =>
            end ? current.isAfter(end) : false
          }
          onChange={(date) => {
            // If the start date is cleared, clear both dates.
            if (!date) {
              onChange && onChange([null, null], ["", ""]);
              return;
            }
            // Otherwise, update with the new start date and current end date.
            onChange &&
              onChange(
                [date, end || null],
                [formatDateString(date), formatDateString(end || null)]
              );
          }}
        />
        <DatePicker
          value={end}
          placeholder="End Date"
          // Disable any date that is before the currently selected start date.
          disabledDate={(current: Dayjs) =>
            start ? current.isBefore(start) : false
          }
          onChange={(date) => {
            // If the end date is cleared, clear both dates.
            if (!date) {
              onChange && onChange([null, null], ["", ""]);
              return;
            }
            // Otherwise, update with the current start date and new end date.
            onChange &&
              onChange(
                [start || null, date],
                [formatDateString(start || null), formatDateString(date)]
              );
          }}
        />
      </div>
    );
  }
};

export { PlanarianDateRange };
