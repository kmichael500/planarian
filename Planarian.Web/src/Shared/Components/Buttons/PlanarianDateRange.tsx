import React from "react";
import { DatePicker, Grid } from "antd";
import type { RangePickerProps } from "antd/lib/date-picker";
import { Dayjs } from "dayjs";

const { RangePicker } = DatePicker;

export interface PlanarianDateRangeProps extends RangePickerProps {}

const PlanarianDateRange: React.FC<PlanarianDateRangeProps> = (props) => {
  const { value = [null, null], onChange, ...restProps } = props;
  const screens = Grid.useBreakpoint();

  if (screens.md) {
    return <RangePicker value={value} onChange={onChange} {...restProps} />;
  } else {
    const [start, end] = value || [null, null];

    const formatDateString = (date: Dayjs | null): string =>
      date ? date.format("YYYY-MM-DD") : "";

    return (
      <div style={{ display: "flex", gap: "8px" }}>
        <DatePicker
          value={start}
          placeholder="Start Date"
          disabledDate={(current: Dayjs) =>
            end ? current.isAfter(end) : false
          }
          onChange={(date) => {
            if (!date) {
              onChange && onChange([null, null], ["", ""]);
              return;
            }
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
          disabledDate={(current: Dayjs) =>
            start ? current.isBefore(start) : false
          }
          onChange={(date) => {
            if (!date) {
              onChange && onChange([null, null], ["", ""]);
              return;
            }
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
