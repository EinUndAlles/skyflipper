'use client';

import { Form } from 'react-bootstrap';
import { FilterOptions } from '@/types/filters';

interface Props {
    onChange(value: string): void;
    options: FilterOptions;
    defaultValue?: any;
    isValid?: boolean;
}

export function NumericalFilterElement({ onChange, options, defaultValue, isValid }: Props) {
    const parseValue = (val: string): number => {
        if (!val) return 0;
        return parseInt(val);
    };

    const value = parseValue(defaultValue);

    return (
        <Form.Control
            type="number"
            value={value}
            onChange={(e) => onChange(e.target.value)}
            isInvalid={!isValid}
        />
    );
}
