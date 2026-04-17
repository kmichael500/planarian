import React from "react";
import styled from "styled-components";
import { Card } from "antd";


const GridCard = styled(Card)`
  && {
    /* Ensure dark mode background for Card */
    background: var(--background-color) !important;
    color: var(--text-color) !important;
    border-color: var(--header-border-color) !important;
  }
  && .ant-card-actions {
    margin-top: auto;
  }
  && .ant-card-body {
    background: transparent !important;
    color: var(--text-color) !important;
  }
  && .ant-card-head {
    background: transparent !important;
    color: var(--text-color) !important;
    border-color: var(--header-border-color) !important;
  }
`

export { GridCard };
