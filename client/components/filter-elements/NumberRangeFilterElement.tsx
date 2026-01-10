'use client';

import { ChangeEvent, useMemo, useState } from 'react';
import Slider from 'rc-slider';
import styles from './NumberRangeFilterElement.module.css';
import 'rc-slider/assets/index.css';
import { Form } from 'react-bootstrap';

interface Props {
    onChange(value: string): void;
    min?: number;
    max?: number;
    defaultValue: any;
}

export function NumberRangeFilterElement({ onChange, min, max, defaultValue }: Props) {
    const parsedValue = useMemo(() => {
        return parseValue(defaultValue);
    }, [defaultValue]);

    const [value, setValue] = useState(parsedValue);
    const [textValue, setTextValue] = useState(defaultValue);

    function parseValue(val: string | number | string[] | number[]): number[] {
        if (!val) {
            return [min || 0, max || 5];
        }

        if (Array.isArray(val)) {
            return val.map(v => parseInt(v.toString()));
        }

        let checks = [
            {
                regexp: new RegExp(/^\d+-\d+$/),
                handler: value => value.split('-').map(v => parseInt(v))
            },
            {
                regexp: new RegExp(/^\d+$/),
                handler: value => [parseInt(value), parseInt(value)]
            },
            {
                regexp: new RegExp(/^<\d+$/),
                handler: value => [min || 0, parseInt(value.split('<')[1]) - 1]
            },
            {
                regexp: new RegExp(/^>\d+$/),
                handler: value => [parseInt(value.split('>')[1]) + 1, max]
            }
        ];

        let result;
        checks.forEach(check => {
            if (value.toString().match(check.regexp)) {
                result = check.handler(value);
            }
        });
        return result || [min || 0, max || 5];
    }

    function _onTextChange(e: ChangeEvent<HTMLInputElement>) {
        setTextValue(e.target.value);
        if (!e.target.value) {
            return;
        }
        let parsed = parseValue(e.target.value);
        if (!parsed) {
            return;
        }
        setValue(parsed);
        onChange(`${parsed[0]}-${parsed[1]}`);
    }

    function _onRangeChange(values: number[]) {
        setTextValue(`${values[0]}-${values[1]}`);
        setValue(values);
        onChange(`${values[0]}-${values[1]}`);
    }

    return (
        <div className={styles.container}>
            <Form.Control value={textValue} onChange={_onTextChange} className={styles.textField} />
            <Slider
                className={styles.slider}
                range
                marks={getMarks()}
                allowCross={false}
                onChange={_onRangeChange}
                min={min || 0}
                max={max}
                value={value}
            />
        </div>
    );
}
